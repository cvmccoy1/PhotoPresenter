using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PhotoPresenter.ViewModels;

public partial class PresentViewModel : ObservableObject
{
    private List<PhotoFolderViewModel> _allFolders = new();
    private int _currentFolderIndex;
    private int _currentPhotoIndex;
    private int _loadSequence;

    // Preload state
    private BitmapImage? _preloadedImage;
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

    public void SetFolders(List<PhotoFolderViewModel> folders)
    {
        _allFolders = folders;
        _currentFolderIndex = 0;
        _currentPhotoIndex = 0;
        _preloadedImage = null;
        _preloadedFolderIndex = -1;
        _preloadedPhotoIndex = -1;
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
        PhotoLabel = $"Photo {_currentPhotoIndex + 1} of {CurrentPhotos.Count}";
    }

    private async Task LoadCurrentPhotoAsync()
    {
        var seq = ++_loadSequence;
        var photos = CurrentPhotos;
        if (photos.Count == 0) return;

        var photo = photos[_currentPhotoIndex];

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
                UpdateLabels();
                _ = PreloadNextAsync();
            }
        }
        catch
        {
            if (seq == _loadSequence) CurrentImage = null;
        }
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

        // Only one photo total — nothing to preload
        if (nextFolder == _currentFolderIndex && nextPhoto == _currentPhotoIndex) return;

        var path = _allFolders[nextFolder].Photos[nextPhoto].FullPath;
        try
        {
            var bmp = await Task.Run(() => PhotoItemViewModel.LoadBitmap(path, 0));
            _preloadedImage = bmp;
            _preloadedFolderIndex = nextFolder;
            _preloadedPhotoIndex = nextPhoto;
        }
        catch { }
    }
}
