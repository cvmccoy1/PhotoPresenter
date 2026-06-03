using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoPresenter.Models;

namespace PhotoPresenter.ViewModels;

public partial class PhotoItemViewModel : ObservableObject
{
    // Limits concurrent thumbnail decodes so Present mode can always get a thread pool thread.
    internal static readonly SemaphoreSlim ThumbSemaphore = new(6, 6);

    public PhotoItem Model { get; }
    public string FileName => Model.FileName;
    public string FullPath => Model.FullPath;
    public bool IsVideo => Model.IsVideo;

    [ObservableProperty] private ImageSource? _thumbnail;
    [ObservableProperty] private bool _isRemoved;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCaption))]
    private string _caption = "";

    public bool HasCaption => !string.IsNullOrEmpty(Caption);

    partial void OnIsRemovedChanged(bool value) => Model.IsRemoved = value;
    partial void OnCaptionChanged(string value)  => Model.Caption  = value;

    public PhotoItemViewModel(PhotoItem model)
    {
        Model    = model;
        _isRemoved = model.IsRemoved;
        _caption   = model.Caption;
        _ = LoadThumbnailAsync();
    }

    private async Task LoadThumbnailAsync()
    {
        if (Model.IsVideo || !File.Exists(Model.FullPath)) return;
        try
        {
            await ThumbSemaphore.WaitAsync();
            try
            {
                var bmp = await Task.Run(() => LoadBitmap(Model.FullPath, 150));
                Thumbnail = bmp;
            }
            finally
            {
                ThumbSemaphore.Release();
            }
        }
        catch { }
    }

    internal static BitmapSource LoadBitmap(string path, int decodeWidth)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var ext = Path.GetExtension(path);
        if (ext.Equals(".heic", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".heif", StringComparison.OrdinalIgnoreCase))
            return LoadViaDecoder(stream, decodeWidth);

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

    // Used for formats whose WIC codec may not honour DecodePixelWidth (e.g. HEIC).
    private static BitmapSource LoadViaDecoder(Stream stream, int decodeWidth)
    {
        var decoder = BitmapDecoder.Create(stream,
            BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        if (decodeWidth > 0 && frame.PixelWidth > decodeWidth)
        {
            double scale = (double)decodeWidth / frame.PixelWidth;
            var scaled = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
            scaled.Freeze();
            return scaled;
        }

        frame.Freeze();
        return frame;
    }
}
