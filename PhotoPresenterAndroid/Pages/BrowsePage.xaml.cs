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
            _ = LoadAllThumbnailsAsync();
        }

        ScrollToCurrentItem();
    }

    private void BuildThumbnailItems()
    {
        _thumbnailItems.Clear();
        for (int i = 0; i < _items.Count; i++)
            _thumbnailItems.Add(new ThumbnailItem(_items[i], i + 1));
        ThumbnailGrid.ItemsSource = _thumbnailItems;
    }

    // Pre-decodes every thumbnail (photo and video) to display size and stores
    // the result as JPEG bytes in ThumbnailItem. On scroll-back, MAUI reads from
    // the in-memory MemoryStream instead of re-reading from disk at full resolution.
    private async Task LoadAllThumbnailsAsync()
    {
        VideoLoadIndicator.IsVisible = true;
        VideoLoadIndicator.IsRunning = true;

        int targetPx = GetThumbnailTargetWidth();
        var semaphore = new SemaphoreSlim(4, 4);

        var tasks = _thumbnailItems.Select(async t =>
        {
            await semaphore.WaitAsync();
            try
            {
                var bytes = await Task.Run(() =>
                    t.IsVideo
                        ? ExtractVideoThumbnailBytes(t.Item.FullPath)
                        : ExtractPhotoThumbnailBytes(t.Item.FullPath, targetPx));
                if (bytes != null)
                    t.SetThumbnailBytes(bytes);
            }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(tasks);

        VideoLoadIndicator.IsRunning = false;
        VideoLoadIndicator.IsVisible = false;
    }

    private static int GetThumbnailTargetWidth()
    {
#if ANDROID
        var metrics = Android.App.Application.Context.Resources?.DisplayMetrics;
        if (metrics != null)
            return metrics.WidthPixels / 3;
#endif
        return 300;
    }

    private static byte[]? ExtractPhotoThumbnailBytes(string path, int targetWidth)
    {
#if ANDROID
        try
        {
            // First pass: get image dimensions without decoding any pixels.
            var bounds = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
            Android.Graphics.BitmapFactory.DecodeFile(path, bounds);
            if (bounds.OutWidth <= 0) return null;

            // Decode at a power-of-two fraction of original size.
            var decoded = Android.Graphics.BitmapFactory.DecodeFile(path,
                new Android.Graphics.BitmapFactory.Options
                {
                    InSampleSize = CalculateInSampleSize(bounds.OutWidth, targetWidth)
                });
            if (decoded == null) return null;

            // BitmapFactory does not auto-apply EXIF orientation; fix it manually.
            // HEIC/HEIF files from phone cameras almost always carry an orientation tag.
            var rotated = ApplyExifOrientation(decoded, path);
            var toCompress = rotated ?? decoded;
            try
            {
                using var stream = new MemoryStream();
                toCompress.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 80, stream);
                return stream.ToArray();
            }
            finally
            {
                decoded.Dispose();
                rotated?.Dispose();
            }
        }
        catch { return null; }
#else
        return null;
#endif
    }

    private static int CalculateInSampleSize(int sourceWidth, int targetWidth)
    {
        int s = 1;
        while (sourceWidth / (s * 2) >= targetWidth)
            s *= 2;
        return s;
    }

#if ANDROID
    // Returns a rotation-corrected copy of the bitmap, or null if no rotation is needed.
    // Handles all 8 EXIF orientation values including mirror variants.
    private static Android.Graphics.Bitmap? ApplyExifOrientation(
        Android.Graphics.Bitmap source, string path)
    {
        try
        {
            using var exif = new Android.Media.ExifInterface(path);
            int orientation = exif.GetAttributeInt(
                Android.Media.ExifInterface.TagOrientation, 1);
            if (orientation <= 1) return null;

            using var matrix = new Android.Graphics.Matrix();
            switch (orientation)
            {
                case 2: matrix.SetScale(-1f,  1f); break;                        // flip H
                case 3: matrix.SetRotate(180f); break;
                case 4: matrix.SetScale( 1f, -1f); break;                        // flip V
                case 5: matrix.SetRotate(270f); matrix.PostScale(-1f, 1f); break; // transpose
                case 6: matrix.SetRotate(90f);  break;
                case 7: matrix.SetRotate(90f);  matrix.PostScale(-1f, 1f); break; // transverse
                case 8: matrix.SetRotate(270f); break;
                default: return null;
            }
            return Android.Graphics.Bitmap.CreateBitmap(
                source, 0, 0, source.Width, source.Height, matrix, true);
        }
        catch { return null; }
    }
#endif

    private static byte[]? ExtractVideoThumbnailBytes(string path)
    {
#if ANDROID
        try
        {
            using var retriever = new Android.Media.MediaMetadataRetriever();
            retriever.SetDataSource(path);

            // HEVC-encoded videos (e.g. iPhone MOV) may not have a sync frame at t=0.
            // Fall back through progressively looser options before giving up.
            var frame = retriever.GetFrameAtTime(0, Android.Media.Option.ClosestSync)
                     ?? retriever.GetFrameAtTime(0, Android.Media.Option.Closest)
                     ?? retriever.GetFrameAtTime(1_000_000, Android.Media.Option.Closest);
            if (frame == null) return null;

            // Apply video rotation metadata (MediaMetadataRetriever does not auto-rotate frames).
            var rotated = ApplyVideoRotation(frame, retriever);
            var toCompress = rotated ?? frame;
            try
            {
                using var stream = new MemoryStream();
                toCompress.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 70, stream);
                return stream.ToArray();
            }
            finally
            {
                frame.Dispose();
                rotated?.Dispose();
            }
        }
        catch { return null; }
#else
        return null;
#endif
    }

#if ANDROID
    private static Android.Graphics.Bitmap? ApplyVideoRotation(
        Android.Graphics.Bitmap source, Android.Media.MediaMetadataRetriever retriever)
    {
        try
        {
            var rotStr = retriever.ExtractMetadata(Android.Media.MetadataKey.VideoRotation);
            if (!int.TryParse(rotStr, out int degrees) || degrees == 0) return null;

            using var matrix = new Android.Graphics.Matrix();
            matrix.SetRotate(degrees);
            return Android.Graphics.Bitmap.CreateBitmap(
                source, 0, 0, source.Width, source.Height, matrix, true);
        }
        catch { return null; }
    }
#endif

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
    // Stores decoded thumbnail as JPEG bytes so that scroll-back reads from
    // memory (MemoryStream) rather than re-decoding the full-resolution file.

    private sealed class ThumbnailItem : INotifyPropertyChanged
    {
        private byte[]? _bytes;
        private ImageSource? _thumbnail;

        public MediaItem Item { get; }
        public int Number { get; }
        public bool IsVideo => Item.IsVideo;

        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            private set
            {
                _thumbnail = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            }
        }

        // Called from the background loading task once bytes are ready.
        public void SetThumbnailBytes(byte[] bytes)
        {
            _bytes = bytes;
            // Factory closure reads from the same in-memory byte array each time —
            // no disk I/O on scroll-back even if MAUI re-invokes the factory.
            Thumbnail = ImageSource.FromStream(() => new MemoryStream(_bytes!));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ThumbnailItem(MediaItem item, int number)
        {
            Item = item;
            Number = number;
        }
    }
}
