using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using PhotoPresenterAndroid.Models;

namespace PhotoPresenterAndroid.Pages;

[QueryProperty(nameof(Items), "Items")]
public partial class PresentPage : ContentPage
{
    private List<MediaItem> _items = [];
    private int _index;
    private double _scale = 1.0;
    private double _startScale = 1.0;
    private double _panX;
    private double _panY;
    private bool _isAutoplay;
    private IDispatcherTimer? _timer;
    private const int AutoplayIntervalSeconds = 5;

    public List<MediaItem> Items
    {
        set
        {
            _items = value ?? [];
            _index = 0;
        }
    }

    public PresentPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // Gesture recognizers wired in code-behind so they share state.
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
        RootGrid.GestureRecognizers.Clear();
        if (VideoPlayer.CurrentState == MediaElementState.Playing)
            VideoPlayer.Pause();
        VideoPlayer.Source = null;
    }

    private void ShowItem(int index)
    {
        if (_items.Count == 0) return;
        _index = Math.Clamp(index, 0, _items.Count - 1);
        var item = _items[_index];

        CounterLabel.Text = $"{_index + 1} / {_items.Count}";

        ResetZoomPan();

        if (item.IsVideo)
        {
            _timer?.Stop();
            PhotoImage.IsVisible = false;
            VideoPlayer.Source = MediaSource.FromFile(item.FullPath);
            VideoPlayer.IsVisible = true;
        }
        else
        {
            VideoPlayer.Source = null;
            VideoPlayer.IsVisible = false;
            PhotoImage.Source = ImageSource.FromFile(item.FullPath);
            PhotoImage.IsVisible = true;

            if (_isAutoplay)
            {
                _timer!.Stop();
                _timer.Start();
            }
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
        _scale = 1.0;
        _panX = 0;
        _panY = 0;
        PhotoImage.Scale = 1;
        PhotoImage.TranslationX = 0;
        PhotoImage.TranslationY = 0;
    }

    private void VideoPlayer_MediaEnded(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(NextItem);
    }

    private void AutoplayButton_Clicked(object sender, EventArgs e)
    {
        _isAutoplay = !_isAutoplay;
        AutoplayButton.Text = _isAutoplay ? "Auto ⏸" : "Auto ▶";

        if (_isAutoplay)
        {
            if (!_items[_index].IsVideo)
                _timer?.Start();
        }
        else
        {
            _timer?.Stop();
        }
    }

    private async void BackButton_Clicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        // Panning when zoomed in; swiping to navigate when at 1×.
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
            // Swipe navigation (only fires on completion).
            if (e.StatusType == GestureStatus.Completed)
            {
                if (e.TotalX < -60)
                    NextItem();
                else if (e.TotalX > 60)
                    PreviousItem();
            }
        }
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            _startScale = _scale;
        }
        else if (e.Status == GestureStatus.Running)
        {
            _scale = Math.Clamp(_startScale * e.Scale, 1.0, 5.0);
            PhotoImage.Scale = _scale;
        }
        else if (e.Status == GestureStatus.Completed && _scale < 1.1)
        {
            // Snap back to fit if barely zoomed in.
            _scale = 1.0;
            PhotoImage.Scale = 1.0;
            _panX = 0;
            _panY = 0;
            PhotoImage.TranslationX = 0;
            PhotoImage.TranslationY = 0;
        }
    }
}
