using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoPresenter.Models;

namespace PhotoPresenter.ViewModels;

public partial class PhotoItemViewModel : ObservableObject
{
    public PhotoItem Model { get; }
    public string FileName => Model.FileName;
    public string FullPath => Model.FullPath;

    [ObservableProperty] private ImageSource? _thumbnail;
    [ObservableProperty] private bool _isRemoved;

    partial void OnIsRemovedChanged(bool value) => Model.IsRemoved = value;

    public PhotoItemViewModel(PhotoItem model)
    {
        Model = model;
        _isRemoved = model.IsRemoved;
        _ = LoadThumbnailAsync();
    }

    private async Task LoadThumbnailAsync()
    {
        if (!File.Exists(Model.FullPath)) return;
        try
        {
            var bmp = await Task.Run(() => LoadBitmap(Model.FullPath, 150));
            Thumbnail = bmp;
        }
        catch { }
    }

    internal static BitmapImage LoadBitmap(string path, int decodeWidth)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = stream;
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
