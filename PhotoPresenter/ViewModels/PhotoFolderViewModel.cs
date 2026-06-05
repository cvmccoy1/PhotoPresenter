using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoPresenter.Models;
using PhotoPresenter.Services;

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
        Model      = model;
        _isRemoved = model.IsRemoved;

        // model.Photos is ordered: active first, then removed at end.
        foreach (var p in model.Photos)
        {
            var vm = new PhotoItemViewModel(p);
            AllPhotoItems.Add(vm);
            if (!p.IsRemoved)
                Photos.Add(vm);
        }

        // Folder card thumbnail — fires immediately; only ~40 folders so cost is negligible.
        var thumbPath = model.Photos.FirstOrDefault(p => !p.IsRemoved && !p.IsVideo)?.FullPath
                     ?? model.Photos.FirstOrDefault(p => !p.IsRemoved)?.FullPath
                     ?? model.Photos.FirstOrDefault()?.FullPath;
        if (thumbPath != null)
            _ = LoadFolderThumbnailAsync(thumbPath);
    }

    // Called by OrganiseViewModel when this folder is selected.
    public void LoadPhotoThumbnails()
    {
        foreach (var photo in AllPhotoItems)
            photo.EnsureThumbnailLoaded();
    }

    private async Task LoadFolderThumbnailAsync(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var cached = await Task.Run(() => ThumbnailCache.TryGet(path + "|80"));
            if (cached != null) { Thumbnail = cached; return; }

            await PhotoItemViewModel.ThumbSemaphore.WaitAsync();
            try
            {
                var bmp = await Task.Run(() => PhotoItemViewModel.LoadBitmap(path, 80));
                Thumbnail = bmp;
                _ = Task.Run(() => ThumbnailCache.Save(path + "|80", bmp));
            }
            finally { PhotoItemViewModel.ThumbSemaphore.Release(); }
        }
        catch { }
    }
}
