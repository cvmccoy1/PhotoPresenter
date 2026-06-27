using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using PhotoPresenterAndroid.Models;
using PhotoPresenterAndroid.Services;

namespace PhotoPresenterAndroid.Pages;

public partial class PresentPage : ContentPage, IQueryAttributable
{
    private List<MediaItem> _items = [];
    private int _index;
    private double _scale = 1.0;
    private double _panX;
    private double _panY;
    private bool _isAutoplay;
    private bool _videoEnded;
    private IDispatcherTimer? _timer;
    private const int AutoplayIntervalSeconds = 5;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Items", out var val) && val is List<MediaItem> items)
            _items = items;
        _index = query.TryGetValue("StartIndex", out var si) && si is int startIdx
            ? PresentationUtils.ClampIndex(startIdx, _items.Count)
            : 0;
    }

    public PresentPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

#if ANDROID
        SetupAndroidGestures();
#else
        SetupMauiGestures();
#endif

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(AutoplayIntervalSeconds);
        _timer.Tick += (_, _) => NextItem();

        ShowItem(_index);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        _timer?.Stop();
        _timer = null;
        VideoPlayer.Stop();
        VideoPlayer.Source = null;
#if ANDROID
        CleanupAndroidGestures();
#else
        GestureOverlay.GestureRecognizers.Clear();
#endif
    }

    private void ShowItem(int index)
    {
        if (_items.Count == 0) return;
        _index = Math.Clamp(index, 0, _items.Count - 1);
        var item = _items[_index];

        _videoEnded = false;
        LoadingLabel.IsVisible = false;
        CounterLabel.Text = $"{_index + 1} / {_items.Count}";
        Preferences.Default.Set("LastPresentedFile", item.FullPath);
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

        CaptionLabel.Text = item.Caption ?? "";
        CaptionBorder.IsVisible = !string.IsNullOrEmpty(item.Caption);
    }

    internal void NextItem()
    {
        if (_items.Count == 0) return;
        ShowItem(PresentationUtils.NextIndex(_index, _items.Count));
    }

    internal void PreviousItem()
    {
        if (_items.Count == 0) return;
        ShowItem(PresentationUtils.PreviousIndex(_index, _items.Count));
    }

    internal void ResetZoomPan()
    {
        _scale = 1.0; _panX = 0; _panY = 0;
        PhotoImage.Scale = 1; PhotoImage.TranslationX = 0; PhotoImage.TranslationY = 0;
        VideoPlayer.Scale = 1; VideoPlayer.TranslationX = 0; VideoPlayer.TranslationY = 0;
    }

    internal void ApplyZoom(double scale)
    {
        PhotoImage.Scale = scale;
        VideoPlayer.Scale = scale;
    }

    internal void ApplyPan(double x, double y)
    {
        PhotoImage.TranslationX = x;
        PhotoImage.TranslationY = y;
        VideoPlayer.TranslationX = x;
        VideoPlayer.TranslationY = y;
    }

    private void VideoPlayer_MediaEnded(object? sender, EventArgs e)
    {
        _videoEnded = true;
        if (_isAutoplay)
            MainThread.BeginInvokeOnMainThread(NextItem);
    }

    // Single-tap on a video: pause if playing, resume if paused, restart if ended.
    internal async void HandleVideoTap()
    {
        var action = PresentationUtils.GetVideoTapAction(
            _videoEnded, VideoPlayer.CurrentState == MediaElementState.Playing);
        switch (action)
        {
            case PresentationUtils.VideoTapAction.Restart:
                _videoEnded = false;
                await VideoPlayer.SeekTo(TimeSpan.Zero);
                VideoPlayer.Play();
                break;
            case PresentationUtils.VideoTapAction.Pause:
                VideoPlayer.Pause();
                break;
            default:
                VideoPlayer.Play();
                break;
        }
    }

    private void AutoplayButton_Clicked(object sender, EventArgs e)
    {
        _isAutoplay = !_isAutoplay;
        AutoplayButton.Text = _isAutoplay ? "Auto ⏸" : "Auto ▶";
        if (_isAutoplay && !_items[_index].IsVideo) _timer?.Start();
        else _timer?.Stop();
    }

    private async void BackButton_Clicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("..",
               new Dictionary<string, object> { ["LastIndex"] = _index });

    // ── Android-native gesture handling ─────────────────────────────────────
    // MAUI's PanGestureRecognizer and PinchGestureRecognizer conflict on
    // Android: two-finger spread is swallowed by the pan recognizer. Android's
    // ScaleGestureDetector + GestureDetector cooperate on the same touch stream,
    // so we bypass MAUI's gesture layer entirely for Android.
#if ANDROID
    private Android.Views.ScaleGestureDetector? _scaleDetector;
    private Android.Views.GestureDetector? _gestureDetector;
    private Android.Views.View? _nativeOverlay;
    internal bool _wasScaling;
    internal float _density = 1f;

    private void SetupAndroidGestures()
    {
        if (GestureOverlay.Handler?.PlatformView is not Android.Views.View nativeView)
            return;

        _nativeOverlay = nativeView;
        _density = nativeView.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        var ctx = nativeView.Context!;
        _scaleDetector = new Android.Views.ScaleGestureDetector(ctx, new PinchListener(this));
        var flingListener = new FlingScrollListener(this);
        _gestureDetector = new Android.Views.GestureDetector(ctx, flingListener);
        _gestureDetector.SetOnDoubleTapListener(flingListener);

        nativeView.Touch += OnNativeTouch;
    }

    private void CleanupAndroidGestures()
    {
        if (_nativeOverlay != null)
            _nativeOverlay.Touch -= OnNativeTouch;
        _nativeOverlay = null;
        _scaleDetector = null;
        _gestureDetector = null;
    }

    private void OnNativeTouch(object? sender, Android.Views.View.TouchEventArgs e)
    {
        if (e.Event == null) return;
        _scaleDetector?.OnTouchEvent(e.Event);
        if (_scaleDetector?.IsInProgress != true)
            _gestureDetector?.OnTouchEvent(e.Event);
        e.Handled = true;
    }

    private sealed class PinchListener : Java.Lang.Object,
        Android.Views.ScaleGestureDetector.IOnScaleGestureListener
    {
        private readonly PresentPage _page;
        internal PinchListener(PresentPage page) => _page = page;

        public bool OnScaleBegin(Android.Views.ScaleGestureDetector detector)
        {
            _page._wasScaling = true;
            return true;
        }

        public bool OnScale(Android.Views.ScaleGestureDetector detector)
        {
            _page._scale = PresentationUtils.ClampedScale(_page._scale, detector.ScaleFactor);
            _page.ApplyZoom(_page._scale);
            return true;
        }

        public void OnScaleEnd(Android.Views.ScaleGestureDetector detector)
        {
            if (PresentationUtils.ShouldResetZoom(_page._scale))
                _page.ResetZoomPan();
        }
    }

    private sealed class FlingScrollListener : Java.Lang.Object,
        Android.Views.GestureDetector.IOnGestureListener,
        Android.Views.GestureDetector.IOnDoubleTapListener
    {
        private readonly PresentPage _page;
        internal FlingScrollListener(PresentPage page) => _page = page;

        // ── IOnGestureListener ───────────────────────────────────────────────
        public bool OnDown(Android.Views.MotionEvent e) => true;
        public void OnShowPress(Android.Views.MotionEvent e) { }
        public bool OnSingleTapUp(Android.Views.MotionEvent e) => false;
        public void OnLongPress(Android.Views.MotionEvent e) { }

        public bool OnScroll(Android.Views.MotionEvent? e1, Android.Views.MotionEvent e2,
            float distanceX, float distanceY)
        {
            if (_page._scale > 1.05)
            {
                // distanceX/Y are in pixels; TranslationX/Y are in DIPs — divide by density.
                _page._panX -= distanceX / _page._density;
                _page._panY -= distanceY / _page._density;
                _page.ApplyPan(_page._panX, _page._panY);
                return true;
            }
            return false;
        }

        public bool OnFling(Android.Views.MotionEvent? e1, Android.Views.MotionEvent? e2,
            float velocityX, float velocityY)
        {
            // Suppress fling that immediately follows a pinch gesture.
            if (_page._wasScaling) { _page._wasScaling = false; return true; }
            if (e1 == null || e2 == null) return false;
            switch (PresentationUtils.GetFlingDirection(velocityX))
            {
                case PresentationUtils.SwipeDirection.Next:     _page.NextItem();     return true;
                case PresentationUtils.SwipeDirection.Previous: _page.PreviousItem(); return true;
                default: return false;
            }
        }

        // ── IOnDoubleTapListener ─────────────────────────────────────────────
        public bool OnDoubleTap(Android.Views.MotionEvent e)
        {
            // Double-tap resets zoom and pan to 1×.
            MainThread.BeginInvokeOnMainThread(() => _page.ResetZoomPan());
            return true;
        }

        public bool OnDoubleTapEvent(Android.Views.MotionEvent e) => false;

        public bool OnSingleTapConfirmed(Android.Views.MotionEvent e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_page._items.Count == 0) return;
                var current = _page._items[_page._index];
                if (current.IsVideo)
                    _page.HandleVideoTap();
                else if (!string.IsNullOrEmpty(current.Caption))
                    _page.CaptionBorder.IsVisible = !_page.CaptionBorder.IsVisible;
            });
            return true;
        }
    }

#else
    // ── Non-Android fallback (MAUI gestures) ────────────────────────────────
    private double _startScale;
    private double _lastSwipeTotalX;
    private double _lastPanTotalX;
    private double _lastPanTotalY;

    private void SetupMauiGestures()
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (_items.Count == 0) return;
            var current = _items[_index];
            if (current.IsVideo)
                HandleVideoTap();
            else if (!string.IsNullOrEmpty(current.Caption))
                CaptionBorder.IsVisible = !CaptionBorder.IsVisible;
        };
        GestureOverlay.GestureRecognizers.Add(tap);

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        GestureOverlay.GestureRecognizers.Add(pan);

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        GestureOverlay.GestureRecognizers.Add(pinch);
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_scale > 1.05)
        {
            if (e.StatusType == GestureStatus.Running)
            {
                _lastPanTotalX = e.TotalX; _lastPanTotalY = e.TotalY;
                ApplyPan(_panX + e.TotalX, _panY + e.TotalY);
            }
            else if (e.StatusType == GestureStatus.Completed)
            {
                _panX += _lastPanTotalX; _panY += _lastPanTotalY;
                _lastPanTotalX = 0; _lastPanTotalY = 0;
            }
        }
        else
        {
            if (e.StatusType == GestureStatus.Running)
                _lastSwipeTotalX = e.TotalX;
            else if (e.StatusType == GestureStatus.Completed)
            {
                switch (PresentationUtils.GetPanSwipeDirection(_lastSwipeTotalX))
                {
                    case PresentationUtils.SwipeDirection.Next:     NextItem();     break;
                    case PresentationUtils.SwipeDirection.Previous: PreviousItem(); break;
                }
                _lastSwipeTotalX = 0;
            }
        }
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started) _startScale = _scale;
        else if (e.Status == GestureStatus.Running)
        {
            _scale = PresentationUtils.ClampedScale(_startScale, e.Scale);
            ApplyZoom(_scale);
        }
        else if (e.Status == GestureStatus.Completed && PresentationUtils.ShouldResetZoom(_scale))
            ResetZoomPan();
    }
#endif
}
