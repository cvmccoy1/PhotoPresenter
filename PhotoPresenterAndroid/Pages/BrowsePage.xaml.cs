using System.ComponentModel;
using PhotoPresenterAndroid.Models;

namespace PhotoPresenterAndroid.Pages;

public partial class BrowsePage : ContentPage, IQueryAttributable
{
    private List<MediaItem> _items = [];
    private int _index;
    private readonly List<ThumbnailItem> _thumbnailItems = [];
    private bool _itemsLoaded;

    public BrowsePage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Forward navigation from MainPage: receive item list and apply session persistence.
        if (query.TryGetValue("Items", out var val) && val is List<MediaItem> items)
        {
            _items = items;
            _itemsLoaded = false; // rebuild thumbnails for new item list
            _thumbnailItems.Clear();

            var lastFile = Preferences.Default.Get("LastPresentedFile", "");
            if (!string.IsNullOrEmpty(lastFile))
            {
                var idx = _items.FindIndex(i => i.FullPath == lastFile);
                // Use saved position unless it was the very last item (treat as "finished" → restart).
                _index = (idx >= 0 && idx < _items.Count - 1) ? idx : 0;
            }
            else
            {
                _index = 0;
            }
        }

        // Back navigation from PresentPage: update position to last-shown item.
        if (query.TryGetValue("LastIndex", out var li) && li is int lastIdx)
            _index = Math.Clamp(lastIdx, 0, Math.Max(0, _items.Count - 1));
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (!_itemsLoaded && _items.Count > 0)
        {
            BuildThumbnailItems();
            _itemsLoaded = true;
            _ = LoadVideoThumbnailsAsync();
        }

        ScrollToCurrentItem();
    }

    private void BuildThumbnailItems()
    {
        _thumbnailItems.Clear();
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            // Photos get their ImageSource immediately (MAUI loads lazily on display).
            // Videos start with null and are filled in by the background task.
            var thumb = item.IsVideo ? null : ImageSource.FromFile(item.FullPath);
            _thumbnailItems.Add(new ThumbnailItem(item, i + 1, thumb));
        }
        ThumbnailGrid.ItemsSource = _thumbnailItems;
    }

    private async Task LoadVideoThumbnailsAsync()
    {
        var videoItems = _thumbnailItems.Where(t => t.IsVideo).ToList();
        if (videoItems.Count == 0) return;

        VideoLoadIndicator.IsVisible = true;
        VideoLoadIndicator.IsRunning = true;

        var semaphore = new SemaphoreSlim(2, 2);
        var tasks = videoItems.Select(async t =>
        {
            await semaphore.WaitAsync();
            try
            {
                var src = await Task.Run(() => ExtractVideoThumbnail(t.Item.FullPath));
                if (src != null)
                    t.Thumbnail = src;
            }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(tasks);

        VideoLoadIndicator.IsRunning = false;
        VideoLoadIndicator.IsVisible = false;
    }

    private static ImageSource? ExtractVideoThumbnail(string path)
    {
#if ANDROID
        try
        {
            using var retriever = new Android.Media.MediaMetadataRetriever();
            retriever.SetDataSource(path);
            using var bitmap = retriever.GetFrameAtTime(0, Android.Media.Option.ClosestSync);
            if (bitmap == null) return null;

            using var stream = new MemoryStream();
            bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 70, stream);
            var bytes = stream.ToArray();
            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch { return null; }
#else
        return null;
#endif
    }

    private void ScrollToCurrentItem()
    {
        if (_thumbnailItems.Count == 0) return;
        var item = _thumbnailItems[_index];
        ThumbnailGrid.SelectedItem = item;

        // Delay scroll slightly so CollectionView finishes layout before ScrollTo is called.
        Dispatcher.DispatchAsync(async () =>
        {
            await Task.Delay(120);
            ThumbnailGrid.ScrollTo(item, animate: false);
        });
    }

    private async void OnTileTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not ThumbnailItem tapped) return;
        ThumbnailGrid.SelectedItem = tapped;
        await Shell.Current.GoToAsync(nameof(PresentPage),
            new Dictionary<string, object>
            {
                ["Items"] = _items,
                ["StartIndex"] = tapped.Number - 1
            });
    }

    // ── ThumbnailItem ────────────────────────────────────────────────────────
    // Wraps a MediaItem with its 1-based display number and a lazily-loaded
    // thumbnail ImageSource. INotifyPropertyChanged lets the CollectionView
    // update live when a video frame arrives from the background task.

    private sealed class ThumbnailItem : INotifyPropertyChanged
    {
        private ImageSource? _thumbnail;

        public MediaItem Item { get; }
        public int Number { get; }
        public bool IsVideo => Item.IsVideo;

        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                _thumbnail = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ThumbnailItem(MediaItem item, int number, ImageSource? thumbnail)
        {
            Item = item;
            Number = number;
            _thumbnail = thumbnail;
        }
    }
}
