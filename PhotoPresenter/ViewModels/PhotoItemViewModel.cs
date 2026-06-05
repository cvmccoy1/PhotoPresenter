using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoPresenter.Models;
using PhotoPresenter.Services;

namespace PhotoPresenter.ViewModels;

public partial class PhotoItemViewModel : ObservableObject
{
    // Scales with core count; Present mode only ever needs 1-2 threads at a time.
    private static readonly int _thumbConcurrency = Math.Max(Environment.ProcessorCount - 2, 4);
    internal static readonly SemaphoreSlim ThumbSemaphore = new(_thumbConcurrency, _thumbConcurrency);

    public PhotoItem Model { get; }
    public string FileName => Model.FileName;
    public string FullPath => Model.FullPath;
    public bool IsVideo => Model.IsVideo;

    [ObservableProperty] private ImageSource? _thumbnail;
    [ObservableProperty] private bool _isRemoved;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCaption))]
    private string _caption = "";

    [ObservableProperty] private string _toolTipText = "";

    public bool HasCaption => !string.IsNullOrEmpty(Caption);

    partial void OnIsRemovedChanged(bool value) => Model.IsRemoved = value;
    partial void OnCaptionChanged(string value)  => Model.Caption  = value;

    private bool _toolTipLoaded;
    private bool _thumbnailRequested;

    public PhotoItemViewModel(PhotoItem model)
    {
        Model      = model;
        _isRemoved = model.IsRemoved;
        _caption   = model.Caption;
        // Thumbnails are loaded on demand via EnsureThumbnailLoaded().
    }

    // Called by PhotoFolderViewModel.LoadPhotoThumbnails() when a folder is selected.
    public void EnsureThumbnailLoaded()
    {
        if (_thumbnailRequested || Thumbnail != null) return;
        _thumbnailRequested = true;
        _ = LoadThumbnailAsync();
    }

    public void EnsureToolTipLoaded()
    {
        if (_toolTipLoaded) return;
        _toolTipLoaded = true;

        var fi = new FileInfo(Model.FullPath);
        string ext    = Path.GetExtension(Model.FileName).ToUpperInvariant();
        string date   = Model.CreationDate == default ? "Unknown" : Model.CreationDate.ToString("yyyy-MM-dd h:mm tt");
        string size   = FormatSize(fi.Exists ? fi.Length : 0);
        string detail = IsVideo ? "Length: …" : "Dimensions: …";

        ToolTipText = $"Type: {ext}\nDate: {date}\n{detail}\nSize: {size}";

        _ = LoadToolTipDetailAsync(fi);
    }

    private async Task LoadToolTipDetailAsync(FileInfo fi)
    {
        string detail;
        if (IsVideo)
        {
            string dur = await Task.Run(() => GetVideoDuration(Model.FullPath));
            detail = $"Length: {dur}";
        }
        else
        {
            string dims = await Task.Run(() => GetPhotoDimensions(Model.FullPath));
            detail = $"Dimensions: {dims}";
        }

        string ext  = Path.GetExtension(Model.FileName).ToUpperInvariant();
        string date = Model.CreationDate == default ? "Unknown" : Model.CreationDate.ToString("yyyy-MM-dd h:mm tt");
        string size = FormatSize(fi.Exists ? fi.Length : 0);
        ToolTipText = $"Type: {ext}\nDate: {date}\n{detail}\nSize: {size}";
    }

    private static string GetPhotoDimensions(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(stream,
                BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnDemand);
            var frame = decoder.Frames[0];
            return $"{frame.PixelWidth} × {frame.PixelHeight}";
        }
        catch { return "Unknown"; }
    }

    private static string GetVideoDuration(string path)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return "Unknown";
            dynamic shell  = Activator.CreateInstance(shellType)!;
            dynamic folder = shell.NameSpace(System.IO.Path.GetDirectoryName(path));
            dynamic item   = folder.ParseName(System.IO.Path.GetFileName(path));
            string dur = folder.GetDetailsOf(item, 27);
            return string.IsNullOrWhiteSpace(dur) ? "Unknown" : dur.Trim();
        }
        catch { return "Unknown"; }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "Unknown";
        if (bytes >= 1_073_741_824L)
            return $"{bytes / 1_073_741_824.0:F2} GB";
        return $"{bytes / 1_048_576.0:F1} MB";
    }

    private async Task LoadThumbnailAsync()
    {
        if (Model.IsVideo || !File.Exists(Model.FullPath)) return;
        try
        {
            // Check cache before acquiring the semaphore — hits are pure disk reads.
            var cached = await Task.Run(() => ThumbnailCache.TryGet(Model.FullPath));
            if (cached != null) { Thumbnail = cached; return; }

            await ThumbSemaphore.WaitAsync();
            try
            {
                var bmp = await Task.Run(() => LoadBitmap(Model.FullPath, 150));
                Thumbnail = bmp;
                _ = Task.Run(() => ThumbnailCache.Save(Model.FullPath, bmp));
            }
            finally { ThumbSemaphore.Release(); }
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
        bmp.CacheOption  = BitmapCacheOption.OnLoad;
        bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

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
