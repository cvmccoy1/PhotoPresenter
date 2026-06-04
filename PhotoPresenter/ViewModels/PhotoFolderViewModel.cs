using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoPresenter.Models;

namespace PhotoPresenter.ViewModels;

public partial class PhotoFolderViewModel : ObservableObject
{
    public PhotoFolder Model { get; }
    public string Name     => Model.Name;
    public string FullPath => Model.FullPath;

    [ObservableProperty] private ImageSource? _thumbnail;
    [ObservableProperty] private bool _isRemoved;

    partial void OnIsRemovedChanged(bool value) => Model.IsRemoved = value;

    // Active photos only — used for Present mode navigation and normal Organise view.
    public ObservableCollection<PhotoItemViewModel> Photos { get; } = new();

    public string FolderToolTipText
    {
        get
        {
            int photos = Photos.Count(p => !p.IsVideo);
            int videos = Photos.Count(p =>  p.IsVideo);
            int total  = photos + videos;
            return $"Photos: {photos}\nVideos: {videos}\nTotal:  {total}";
        }
    }

    // All photos including removed — used for Show All mode.
    public ObservableCollection<PhotoItemViewModel> AllPhotoItems { get; } = new();

    public PhotoFolderViewModel(PhotoFolder model)
    {
        Model = model;
        _isRemoved = model.IsRemoved;

        // model.Photos is ordered: active first, then removed at end.
        foreach (var p in model.Photos)
        {
            var vm = new PhotoItemViewModel(p);
            AllPhotoItems.Add(vm);
            if (!p.IsRemoved)
                Photos.Add(vm);
        }

        // Thumbnail from first active non-video photo; fall back to any active or any photo.
        var thumbPath = model.Photos.FirstOrDefault(p => !p.IsRemoved && !p.IsVideo)?.FullPath
                     ?? model.Photos.FirstOrDefault(p => !p.IsRemoved)?.FullPath
                     ?? model.Photos.FirstOrDefault()?.FullPath;
        if (thumbPath != null)
            _ = LoadThumbnailAsync(thumbPath);
    }

    private async Task LoadThumbnailAsync(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            await PhotoItemViewModel.ThumbSemaphore.WaitAsync();
            try
            {
                var bmp = await Task.Run(() => PhotoItemViewModel.LoadBitmap(path, 80));
                Thumbnail = bmp;
            }
            finally
            {
                PhotoItemViewModel.ThumbSemaphore.Release();
            }
        }
        catch { }
    }
}
