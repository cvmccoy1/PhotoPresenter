using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using PhotoPresenterAndroid.Models;

namespace PhotoPresenterAndroid.Pages;

public partial class PresentPage : ContentPage, IQueryAttributable
{
    private List<MediaItem> _items = [];
    private int _index;
    private double _scale = 1.0;
    private double _startScale = 1.0;
    private double _panX;
    private double _panY;
    private double _lastSwipeTotalX;  // TotalX resets to 0 at Completed on Android
    private bool _isAutoplay;
    private IDispatcherTimer? _timer;
    private const int AutoplayIntervalSeconds = 5;

    // IQueryAttributable guarantees this runs before OnNavigatedTo
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Items", out var val) && val is List<MediaItem> items)
        {
            _items = items;
            _index = 0;
            Android.Util.Log.Debug("PP_DIAG", $"PresentPage.ApplyQueryAttributes: {_items.Count} items");
        }
        else
        {
            Android.Util.Log.Debug("PP_DIAG", "PresentPage.ApplyQueryAttributes: no Items key");
        }
    }

    public PresentPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        Android.Util.Log.Debug("PP_DIAG", $"PresentPage.OnNavigatedTo: {_items.Count} items");

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        RootGrid.GestureRecognizers.Add(pan);

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        RootGrid.GestureRecognizers.Add(pinch);

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(AutoplayIntervalSeconds);
        _timer.Tick += (_, _) => NextItem();

        ShowItem(0);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        _timer?.Stop();
        _timer = null;
        VideoPlayer.Stop();
        VideoPlayer.Source = null;
        RootGrid.GestureRecognizers.Clear();
    }

    private void ShowItem(int index)
    {
        if (_items.Count == 0) return;
        _index = Math.Clamp(index, 0, _items.Count - 1);
        var item = _items[_index];

        Android.Util.Log.Debug("PP_DIAG", $"ShowItem {_index}: {item.FullPath} isVideo={item.IsVideo}");

        LoadingLabel.IsVisible = false;
        CounterLabel.Text = $"{_index + 1} / {_items.Count}";
        ResetZoomPan();

        if (item.IsVideo)
        {
            _timer?.Stop();
            PhotoImage.IsVisible = false;
            PhotoImage.Source = null;
            VideoPlayer.Source = MediaSource.FromFile(item.FullPath);
            VideoPlayer.IsVisible = true;
            VideoPlayer.Play();
        }
        else
        {
            VideoPlayer.Stop();
            VideoPlayer.Source = null;
            VideoPlayer.IsVisible = false;
            PhotoImage.Source = ImageSource.FromFile(item.FullPath);
            PhotoImage.IsVisible = true;
            if (_isAutoplay) { _timer!.Stop(); _timer.Start(); }
        }

        bool hasCaption = !string.IsNullOrEmpty(item.Caption);
        CaptionLabel.Text = item.Caption ?? "";
        CaptionBorder.IsVisible = hasCaption;
    }

    private void NextItem()
    {
        if (_items.Count == 0) return;
        ShowItem((_index + 1) % _items.Count);
    }

    private void PreviousItem()
    {
        if (_items.Count == 0) return;
        ShowItem((_index - 1 + _items.Count) % _items.Count);
    }

    private void ResetZoomPan()
    {
        _scale = 1.0; _panX = 0; _panY = 0;
        PhotoImage.Scale = 1;
        PhotoImage.TranslationX = 0;
        PhotoImage.TranslationY = 0;
    }

    private void VideoPlayer_MediaEnded(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(NextItem);

    private void AutoplayButton_Clicked(object sender, EventArgs e)
    {
        _isAutoplay = !_isAutoplay;
        AutoplayButton.Text = _isAutoplay ? "Auto ⏸" : "Auto ▶";
        if (_isAutoplay && !_items[_index].IsVideo) _timer?.Start();
        else _timer?.Stop();
    }

    private async void BackButton_Clicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_scale > 1.05)
        {
            if (e.StatusType == GestureStatus.Running)
            {
                PhotoImage.TranslationX = _panX + e.TotalX;
                PhotoImage.TranslationY = _panY + e.TotalY;
            }
            else if (e.StatusType == GestureStatus.Completed)
            {
                _panX = PhotoImage.TranslationX;
                _panY = PhotoImage.TranslationY;
            }
        }
        else
        {
            // Track TotalX during Running because Android resets it to 0 at Completed.
            if (e.StatusType == GestureStatus.Running)
                _lastSwipeTotalX = e.TotalX;
            else if (e.StatusType == GestureStatus.Completed)
            {
                if (_lastSwipeTotalX < -60) NextItem();
                else if (_lastSwipeTotalX > 60) PreviousItem();
                _lastSwipeTotalX = 0;
            }
        }
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
            _startScale = _scale;
        else if (e.Status == GestureStatus.Running)
        {
            _scale = Math.Clamp(_startScale * e.Scale, 1.0, 5.0);
            PhotoImage.Scale = _scale;
        }
        else if (e.Status == GestureStatus.Completed && _scale < 1.1)
        {
            _scale = 1.0; PhotoImage.Scale = 1.0;
            _panX = 0; _panY = 0;
            PhotoImage.TranslationX = 0; PhotoImage.TranslationY = 0;
        }
    }
}
