using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace PhotoPresenter.Services;

internal static class ThumbnailCache
{
    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhotoPresenter", "thumbcache");

    static ThumbnailCache()
    {
        Directory.CreateDirectory(_dir);
        Task.Run(Cleanup);
    }

    private static string KeyPath(string src)
    {
        var ticks = File.GetLastWriteTimeUtc(src).Ticks;
        var hash  = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(src)));
        return Path.Combine(_dir, $"{hash}_{ticks}.jpg");
    }

    internal static BitmapSource? TryGet(string src)
    {
        try
        {
            var p = KeyPath(src);
            if (!File.Exists(p)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource   = new Uri(p);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    internal static void Save(string src, BitmapSource bmp)
    {
        try
        {
            var enc = new JpegBitmapEncoder { QualityLevel = 85 };
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var s = File.Create(KeyPath(src));
            enc.Save(s);
        }
        catch { }
    }

    private static void Cleanup()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-90);
            foreach (var f in Directory.EnumerateFiles(_dir, "*.jpg"))
                if (File.GetLastWriteTimeUtc(f) < cutoff)
                    try { File.Delete(f); } catch { }

            // Per source-path hash, keep only the newest ticks version; delete stale ones.
            Directory.EnumerateFiles(_dir, "*.jpg")
                .Select(f => (Path: f, Name: Path.GetFileNameWithoutExtension(f)))
                .Where(x => x.Name.Contains('_'))
                .GroupBy(x => x.Name[..x.Name.LastIndexOf('_')])
                .SelectMany(g => g.OrderByDescending(x => x.Name).Skip(1))
                .ToList()
                .ForEach(x => { try { File.Delete(x.Path); } catch { } });
        }
        catch { }
    }
}
