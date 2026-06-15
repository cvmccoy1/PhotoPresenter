using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Views;

public partial class PresentView : UserControl
{
    private readonly DispatcherTimer _positionTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private double _videoDurationSeconds;
    private bool _isDragging;
    private bool _isTrackClick;
    private bool _mediaFailed;

    public PresentView()
    {
        InitializeComponent();
        _positionTimer.Tick += PositionTimer_Tick;
        DataContextChanged += OnDataContextChanged;

        ScrubSlider.AddHandler(Thumb.DragStartedEvent,
            new DragStartedEventHandler(ScrubSlider_DragStarted));
        ScrubSlider.AddHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler(ScrubSlider_DragCompleted));
        ScrubSlider.PreviewMouseLeftButtonDown += ScrubSlider_PreviewMouseLeftButtonDown;
        ScrubSlider.PreviewMouseLeftButtonUp   += ScrubSlider_PreviewMouseLeftButtonUp;
        ScrubSlider.ValueChanged               += ScrubSlider_ValueChanged;
    }

    private PresentViewModel? Vm => DataContext as PresentViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PresentViewModel oldVm)
            oldVm.PropertyChanged -= OnVmPropertyChanged;
        if (e.NewValue is PresentViewModel newVm)
            newVm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PresentViewModel.CurrentIsVideo):
                if (Vm?.CurrentIsVideo == false)
                {
                    _positionTimer.Stop();
                    VideoPlayer.Stop();
                    VideoPlayer.Source = null;
                    HideVideoError();
                }
                break;

            case nameof(PresentViewModel.CurrentVideoPath):
                var path = Vm?.CurrentVideoPath;
                if (!string.IsNullOrEmpty(path))
                {
                    HideVideoError();
                    _isDragging = false;
                    ScrubSlider.Value = 0;
                    ScrubSlider.Maximum = 100;
                    VideoPlayer.Source = new Uri(path);
                    VideoPlayer.Play();
                    Vm!.IsPlaying = true;
                    _positionTimer.Start();
                }
                break;

            case nameof(PresentViewModel.IsPlaying):
                // Only act if a video is actually loaded
                if (Vm?.CurrentIsVideo == true && VideoPlayer.Source != null)
                {
                    if (Vm.IsPlaying)
                    {
                        VideoPlayer.Play();
                        _positionTimer.Start();
                    }
                    else
                    {
                        VideoPlayer.Pause();
                    }
                }
                break;
        }
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (Vm == null || !VideoPlayer.NaturalDuration.HasTimeSpan) return;
        var pos = VideoPlayer.Position;
        var dur = VideoPlayer.NaturalDuration.TimeSpan;
        if (!_isDragging)
            ScrubSlider.Value = pos.TotalSeconds;
        Vm.UpdatePosition(pos, dur);
    }

    private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (!VideoPlayer.NaturalDuration.HasTimeSpan) return;
        _videoDurationSeconds = VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
        ScrubSlider.Maximum = _videoDurationSeconds > 0 ? _videoDurationSeconds : 100;
        ScrubSlider.Value = 0;
        // Retry Play() — covers timing races and WMF recovery after a partial MediaFailed.
        if (Vm?.IsPlaying == true)
            VideoPlayer.Play();
        if (_mediaFailed || !VideoPlayer.HasAudio)
            ShowVideoError("Playing without audio — audio codec may not be installed");
    }

    private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (Vm != null) Vm.IsPlaying = false;
        _positionTimer.Stop();
    }

    private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _mediaFailed = true;
        ShowVideoError("Unable to play video — codec may not be installed");
        // Don't reset IsPlaying or stop the timer: WMF may still recover and fire MediaOpened,
        // at which point the belt-and-suspenders Play() will auto-start the video track.
    }

    private void ShowVideoError(string message)
    {
        VideoErrorText.Text = message;
        VideoErrorBanner.Visibility = Visibility.Visible;
    }

    private void HideVideoError()
    {
        _mediaFailed = false;
        VideoErrorBanner.Visibility = Visibility.Collapsed;
        VideoErrorText.Text = "";
    }

    // Thumb drag: set flag so timer stops overriding the slider.
    private void ScrubSlider_DragStarted(object sender, DragStartedEventArgs e)
        => _isDragging = true;

    // Thumb drag complete: seek and resume play.
    private void ScrubSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDragging = false;
        VideoPlayer.Position = TimeSpan.FromSeconds(ScrubSlider.Value);
        if (Vm?.IsPlaying == true)
            VideoPlayer.Play();
    }

    // Block the timer and detect whether this is a thumb drag or a track click.
    // For track clicks: jump to the exact clicked position and suppress the
    // RepeatButton's LargeChange command (which caused the jump-to-start/end behaviour).
    private void ScrubSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        if (IsThumbHit(e.OriginalSource as DependencyObject))
        {
            _isTrackClick = false; // DragStarted/DragCompleted will handle this drag.
        }
        else
        {
            _isTrackClick = true;
            if (ScrubSlider.Template.FindName("PART_Track", ScrubSlider) is Track track)
                ScrubSlider.Value = track.ValueFromPoint(e.GetPosition(track));
            e.Handled = true; // Suppress RepeatButton so it can't fire LargeChange.
        }
    }

    // Track click completion. _isTrackClick is false for thumb drags (set in PreviewMouseLeftButtonDown),
    // so this is a no-op for those — DragCompleted fires afterward and handles them.
    private void ScrubSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isTrackClick) return;
        _isTrackClick = false;
        _isDragging = false;
        VideoPlayer.Position = TimeSpan.FromSeconds(ScrubSlider.Value);
        if (Vm?.IsPlaying == true)
            VideoPlayer.Play();
    }

    // Live scrub: seek on each value change while dragging.
    // Also forces WMF to render the seeked frame when the video is paused (WMF does not
    // update the displayed frame on a Position change alone without a Play/Pause cycle).
    private void ScrubSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isDragging) return;
        var pos = TimeSpan.FromSeconds(e.NewValue);
        VideoPlayer.Position = pos;
        if (Vm?.IsPlaying == false && VideoPlayer.Source != null)
        {
            VideoPlayer.Play();
            VideoPlayer.Pause();
        }
        if (VideoPlayer.NaturalDuration.HasTimeSpan)
            Vm?.UpdatePosition(pos, VideoPlayer.NaturalDuration.TimeSpan);
    }

    private static bool IsThumbHit(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Thumb) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (Vm != null) Vm.IsPlaying = !Vm.IsPlaying;
    }

    private void RestartVideo_Click(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Position = TimeSpan.Zero;
        ScrubSlider.Value = 0;
        VideoPlayer.Play();
        if (Vm != null)
        {
            Vm.IsPlaying = true;
            _positionTimer.Start();
        }
    }

    private void RotateVideo_Click(object sender, RoutedEventArgs e)
    {
        Vm?.RotateVideo();
    }

    // Photo pan/zoom — disabled for video
    private void RootGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Vm?.CurrentIsVideo == true) return;
        Vm?.ZoomByDelta(e.Delta);
        e.Handled = true;
    }

    private void RootGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm?.CurrentIsVideo == true) return;
        Vm?.BeginPan(e.GetPosition(this));
        RootGrid.CaptureMouse();
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && Vm?.CurrentIsVideo == false)
            Vm?.UpdatePan(e.GetPosition(this));
    }

    private void RootGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        RootGrid.ReleaseMouseCapture();
    }
}
