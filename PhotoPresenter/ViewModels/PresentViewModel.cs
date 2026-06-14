using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace PhotoPresenter.ViewModels;

public partial class PresentViewModel : ObservableObject
{
    private List<PhotoFolderViewModel> _allFolders = new();
    private int _currentFolderIndex;
    private int _currentPhotoIndex;
    private int _loadSequence;

    // Preload state
    private BitmapSource? _preloadedImage;
    private int _preloadedFolderIndex = -1;
    private int _preloadedPhotoIndex = -1;

    // Pan drag state
    private Point _panStart;
    private double _panStartX;
    private double _panStartY;

    [ObservableProperty] private ImageSource? _currentImage;
    [ObservableProperty] private double _zoomScale = 1.0;
    [ObservableProperty] private double _panX;
    [ObservableProperty] private double _panY;
    [ObservableProperty] private string _folderLabel = "";
    [ObservableProperty] private string _photoLabel = "";

    // Video state
    [ObservableProperty] private bool _currentIsVideo;
    [ObservableProperty] private string _currentVideoPath = "";
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private double _volume = 0.5;
    [ObservableProperty] private string _positionLabel = "";
    [ObservableProperty] private string _playPauseIcon = "▶";
    [ObservableProperty] private double _videoRotation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrentCaption))]
    private string _currentCaption = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MirrorScaleX))]
    private bool _currentIsMirrored;

    public double MirrorScaleX => CurrentIsMirrored ? -1.0 : 1.0;

    public bool HasCurrentCaption => !string.IsNullOrEmpty(CurrentCaption);

    partial void OnIsPlayingChanged(bool value) => PlayPauseIcon = value ? "⏸" : "▶";

    public void RotateVideo() => VideoRotation = (VideoRotation + 90) % 360;

    public void SetFolders(List<PhotoFolderViewModel> folders, int startFolderIndex = 0, int startPhotoIndex = 0)
    {
        _allFolders = folders;
        _currentFolderIndex = folders.Count > 0 ? Math.Clamp(startFolderIndex, 0, folders.Count - 1) : 0;
        var photoCount = CurrentPhotos.Count;
        _currentPhotoIndex = photoCount > 0 ? Math.Clamp(startPhotoIndex, 0, photoCount - 1) : 0;
        _preloadedImage = null;
        _preloadedFolderIndex = -1;
        _preloadedPhotoIndex = -1;
        CurrentIsVideo = false;
        CurrentVideoPath = "";
        IsPlaying = false;
        PositionLabel = "";
        VideoRotation = 0;
        CurrentIsMirrored = false;
        ResetZoomPan();
        UpdateLabels();
        if (_allFolders.Count > 0 && CurrentPhotos.Count > 0)
            _ = LoadCurrentPhotoAsync();
    }

    public void NextPhoto()
    {
        if (_allFolders.Count == 0) return;
        _currentPhotoIndex++;
        if (_currentPhotoIndex >= CurrentPhotos.Count)
        {
            _currentFolderIndex = (_currentFolderIndex + 1) % _allFolders.Count;
            _currentPhotoIndex = 0;
        }
        ResetZoomPan();
        _ = LoadCurrentPhotoAsync();
    }

    public void PreviousPhoto()
    {
        if (_allFolders.Count == 0) return;
        _currentPhotoIndex--;
        if (_currentPhotoIndex < 0)
        {
            _currentFolderIndex = (_currentFolderIndex - 1 + _allFolders.Count) % _allFolders.Count;
            _currentPhotoIndex = Math.Max(0, CurrentPhotos.Count - 1);
        }
        ResetZoomPan();
        _ = LoadCurrentPhotoAsync();
    }

    public void ZoomIn() => ZoomScale = Math.Min(ZoomScale * 1.2, 10.0);
    public void ZoomOut() => ZoomScale = Math.Max(ZoomScale / 1.2, 0.5);
    public void ZoomByDelta(double delta) { if (delta > 0) ZoomIn(); else ZoomOut(); }

    public void BeginPan(Point screenPoint)
    {
        _panStart = screenPoint;
        _panStartX = PanX;
        _panStartY = PanY;
    }

    public void UpdatePan(Point screenPoint)
    {
        PanX = _panStartX + (screenPoint.X - _panStart.X);
        PanY = _panStartY + (screenPoint.Y - _panStart.Y);
    }

    public void UpdatePosition(TimeSpan position, TimeSpan duration)
    {
        PositionLabel = $"{FormatTime(position)} / {FormatTime(duration)}";
    }

    private static string FormatTime(TimeSpan ts) =>
        ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";

    public PhotoFolderViewModel? CurrentFolder =>
        _allFolders.Count > 0 ? _allFolders[_currentFolderIndex] : null;

    public PhotoItemViewModel? CurrentPhotoItem =>
        CurrentPhotos.Count > 0 ? CurrentPhotos[_currentPhotoIndex] : null;

    private ObservableCollection<PhotoItemViewModel> CurrentPhotos =>
        _allFolders.Count > 0 ? _allFolders[_currentFolderIndex].Photos : new();

    private void ResetZoomPan()
    {
        ZoomScale = 1.0;
        PanX = 0;
        PanY = 0;
    }

    private void UpdateLabels()
    {
        if (_allFolders.Count == 0) return;
        FolderLabel = $"{_allFolders[_currentFolderIndex].Name}  ({_currentFolderIndex + 1} of {_allFolders.Count})";
        PhotoLabel = $"Item {_currentPhotoIndex + 1} of {CurrentPhotos.Count}";
    }

    private async Task LoadCurrentPhotoAsync()
    {
        var seq = ++_loadSequence;
        var photos = CurrentPhotos;
        if (photos.Count == 0) return;

        var photo = photos[_currentPhotoIndex];
        CurrentIsMirrored = photo.IsMirrored;

        if (photo.IsVideo)
        {
            if (seq != _loadSequence) return;
            CurrentImage = null;
            CurrentIsVideo = true;
            IsPlaying = false;
            PositionLabel = "";
            VideoRotation = await GetVideoRotationAsync(photo.FullPath);
            CurrentCaption = photo.Caption;
            CurrentVideoPath = photo.FullPath;
            UpdateLabels();
            return;
        }

        // Photo path — clear any previous video state first
        CurrentIsVideo = false;
        CurrentVideoPath = "";
        IsPlaying = false;

        // Use preloaded bitmap if it matches
        if (_preloadedFolderIndex == _currentFolderIndex &&
            _preloadedPhotoIndex == _currentPhotoIndex &&
            _preloadedImage != null)
        {
            var cached = _preloadedImage;
            _preloadedImage = null;
            _preloadedFolderIndex = -1;
            _preloadedPhotoIndex = -1;
            if (seq == _loadSequence)
            {
                CurrentImage = cached;
                CurrentCaption = photo.Caption;
                UpdateLabels();
                _ = PreloadNextAsync();
            }
            return;
        }

        try
        {
            var bmp = await Task.Run(() => PhotoItemViewModel.LoadBitmap(photo.FullPath, 0));
            if (seq == _loadSequence)
            {
                CurrentImage = bmp;
                CurrentCaption = photo.Caption;
                UpdateLabels();
                _ = PreloadNextAsync();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (seq == _loadSequence) CurrentImage = null;
        }
    }

    private static async Task<double> GetVideoRotationAsync(string path)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            var props = await file.Properties.GetVideoPropertiesAsync();
            return props.Orientation switch
            {
                VideoOrientation.Rotate90  => 90,
                VideoOrientation.Rotate180 => 180,
                VideoOrientation.Rotate270 => 270,
                _                          => 0
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return 0; }
    }

    private async Task PreloadNextAsync()
    {
        if (_allFolders.Count == 0) return;

        var nextFolder = _currentFolderIndex;
        var nextPhoto = _currentPhotoIndex + 1;

        if (nextPhoto >= _allFolders[nextFolder].Photos.Count)
        {
            nextFolder = (nextFolder + 1) % _allFolders.Count;
            nextPhoto = 0;
        }

        // Only one item total — nothing to preload
        if (nextFolder == _currentFolderIndex && nextPhoto == _currentPhotoIndex) return;

        var nextItem = _allFolders[nextFolder].Photos[nextPhoto];
        if (nextItem.IsVideo) return; // no preload for video

        var path = nextItem.FullPath;
        try
        {
            var bmp = await Task.Run(() => PhotoItemViewModel.LoadBitmap(path, 0));
            _preloadedImage = bmp;
            _preloadedFolderIndex = nextFolder;
            _preloadedPhotoIndex = nextPhoto;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
    }
}
