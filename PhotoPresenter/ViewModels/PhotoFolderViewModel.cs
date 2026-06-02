using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoPresenter.Models;

namespace PhotoPresenter.ViewModels;

public partial class PhotoFolderViewModel : ObservableObject
{
    public PhotoFolder Model { get; }
    public string Name => Model.Name;
    public string FullPath => Model.FullPath;

    [ObservableProperty]
    private ImageSource? _thumbnail;

    public ObservableCollection<PhotoItemViewModel> Photos { get; } = new();

    public PhotoFolderViewModel(PhotoFolder model)
    {
        Model = model;
        foreach (var p in model.Photos)
            Photos.Add(new PhotoItemViewModel(p));

        if (model.Photos.Count > 0)
            _ = LoadThumbnailAsync(model.Photos[0].FullPath);
    }

    private async Task LoadThumbnailAsync(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var bmp = await Task.Run(() => PhotoItemViewModel.LoadBitmap(path, 80));
            Thumbnail = bmp;
        }
        catch { }
    }
}
