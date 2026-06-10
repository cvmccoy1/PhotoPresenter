using System.Globalization;
using System.IO;
using System.Text.Json;
using PhotoPresenter.Models;

namespace PhotoPresenter.Services;

public class PhotoLibraryService : IPhotoLibraryService
{
    private static readonly HashSet<string> PhotoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".heic", ".heif" };

    private static readonly HashSet<string> VideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mov", ".mp4", ".avi", ".wmv", ".m4v", ".mkv" };

    private static readonly HashSet<string> MediaExtensions =
        new(PhotoExtensions.Concat(VideoExtensions), StringComparer.OrdinalIgnoreCase);

    internal static bool IsMediaFile(string path) => MediaExtensions.Contains(Path.GetExtension(path));
    internal static bool IsVideoFile(string path) => VideoExtensions.Contains(Path.GetExtension(path));

    private const string FolderOrderFile = "_photofolderorder.json";
    private const string PhotoOrderFile  = "_photoorder.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Returns ALL folders: active ones first (in sidecar order), removed ones at the end.
    public async Task<List<PhotoFolder>> LoadLibraryAsync(string parentPath)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(parentPath))
                return new List<PhotoFolder>();

            var subdirs = Directory.GetDirectories(parentPath)
                .Select(d => new DirectoryInfo(d))
                .ToList();

            var (activeDirs, removedNames) = ApplyFolderOrder(parentPath, subdirs);
            var dirLookup = subdirs.ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);

            var result = new List<PhotoFolder>();

            foreach (var dir in activeDirs)
                result.Add(new PhotoFolder
                {
                    Name     = dir.Name,
                    FullPath = dir.FullName,
                    Photos   = LoadPhotosForFolder(dir.FullName)
                });

            foreach (var name in removedNames)
                if (dirLookup.TryGetValue(name, out var dir))
                    result.Add(new PhotoFolder
                    {
                        Name      = dir.Name,
                        FullPath  = dir.FullName,
                        Photos    = LoadPhotosForFolder(dir.FullName),
                        IsRemoved = true
                    });

            return result;
        });
    }

    // Returns (activeDirs in sidecar order, removedNames). Unknown dirs appended to active.
    internal static (List<DirectoryInfo> Active, List<string> RemovedNames) ApplyFolderOrder(
        string parentPath, List<DirectoryInfo> subdirs)
    {
        var sidecarPath = Path.Combine(parentPath, FolderOrderFile);
        if (!File.Exists(sidecarPath))
            return (subdirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList(), new());

        try
        {
            var sidecar = JsonSerializer.Deserialize<FolderOrderSidecar>(File.ReadAllText(sidecarPath));
            if (sidecar?.Order == null)
                return (subdirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList(), new());

            var removedNames = sidecar.Removed ?? new();
            var removedSet   = new HashSet<string>(removedNames, StringComparer.OrdinalIgnoreCase);
            var lookup       = subdirs.ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);
            var ordered      = new List<DirectoryInfo>();

            foreach (var name in sidecar.Order)
                if (lookup.Remove(name, out var dir))
                    ordered.Add(dir);

            // New dirs (not in order and not in removed list) appended alphabetically
            foreach (var dir in lookup.Values
                .Where(d => !removedSet.Contains(d.Name))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                ordered.Add(dir);

            return (ordered, removedNames);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (subdirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList(), new());
        }
    }

    // Returns active photos first (in sidecar order), removed photos at the end.
    internal static List<PhotoItem> LoadPhotosForFolder(string folderPath)
    {
        var files = Directory.GetFiles(folderPath)
            .Where(f => MediaExtensions.Contains(Path.GetExtension(f)))
            .Select(f => new FileInfo(f))
            .ToList();

        var sidecarPath = Path.Combine(folderPath, PhotoOrderFile);
        if (!File.Exists(sidecarPath))
        {
            var items = files.Select(f => ToPhotoItem(f)).ToList();
            return items.OrderBy(p => p.CreationDate).ToList();
        }

        try
        {
            var sidecar = JsonSerializer.Deserialize<PhotoOrderSidecar>(File.ReadAllText(sidecarPath));
            if (sidecar?.Order == null)
            {
                var items = files.Select(f => ToPhotoItem(f)).ToList();
                return items.OrderBy(p => p.CreationDate).ToList();
            }

            var removedNames = sidecar.Removed ?? new();
            var captions     = sidecar.Captions;
            var mirroredSet  = new HashSet<string>(sidecar.Mirrored ?? new(), StringComparer.OrdinalIgnoreCase);
            var removedSet   = new HashSet<string>(removedNames, StringComparer.OrdinalIgnoreCase);
            var allFiles     = files.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
            var mutable      = new Dictionary<string, FileInfo>(allFiles, StringComparer.OrdinalIgnoreCase);
            var result       = new List<PhotoItem>();

            // Active in sidecar order
            foreach (var name in sidecar.Order)
                if (mutable.Remove(name, out var file))
                    result.Add(ToPhotoItem(file, captions: captions, mirrored: mirroredSet));

            // New files (not in sidecar, not removed) appended by effective date
            var newItems = mutable.Values
                .Where(f => !removedSet.Contains(f.Name))
                .Select(f => ToPhotoItem(f))
                .OrderBy(p => p.CreationDate)
                .ToList();
            foreach (var item in newItems)
                result.Add(item);

            // Removed files that still exist on disk
            foreach (var name in removedNames)
                if (allFiles.TryGetValue(name, out var file))
                    result.Add(ToPhotoItem(file, isRemoved: true, captions: captions, mirrored: mirroredSet));

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var items = files.Select(f => ToPhotoItem(f)).ToList();
            return items.OrderBy(p => p.CreationDate).ToList();
        }
    }

    private static PhotoItem ToPhotoItem(FileInfo file, bool isRemoved = false,
        Dictionary<string, string>? captions = null, HashSet<string>? mirrored = null)
    {
        var item = new PhotoItem
        {
            FileName     = file.Name,
            FullPath     = file.FullName,
            CreationDate = GetEffectiveDate(file),
            IsRemoved    = isRemoved,
            IsVideo      = VideoExtensions.Contains(Path.GetExtension(file.Name)),
            IsMirrored   = mirrored?.Contains(file.Name) == true
        };
        if (captions != null && captions.TryGetValue(file.Name, out var cap))
            item.Caption = cap;
        return item;
    }

    // Fast filesystem-only date used during library load.
    private static DateTime GetEffectiveDate(FileInfo file) =>
        file.LastWriteTime < file.CreationTime ? file.LastWriteTime : file.CreationTime;

    // EXIF-aware date used only when the user explicitly sorts by date.
    // Matches File Explorer: prefer EXIF Date Taken for photos, else LastWriteTime.
    public DateTime GetEffectiveDateWithExif(PhotoItem item)
    {
        if (!item.IsVideo)
        {
            var exif = TryGetExifDate(item.FullPath);
            if (exif.HasValue) return exif.Value;
        }
        var file = new FileInfo(item.FullPath);
        return file.Exists ? file.LastWriteTime : item.CreationDate;
    }

    // Reads EXIF DateTaken from an image file without decoding pixels.
    private static DateTime? TryGetExifDate(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(stream,
                BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnDemand);

            if (decoder.Frames[0].Metadata is not BitmapMetadata meta) return null;

            var raw = meta.DateTaken;
            if (string.IsNullOrEmpty(raw)) return null;

            // Standard EXIF format: "YYYY:MM:DD HH:MM:SS"
            if (DateTime.TryParseExact(raw, "yyyy:MM:dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;

            // Fall back to general parse for non-standard formats
            if (DateTime.TryParse(raw, out var dt2))
                return dt2;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
        return null;
    }

    // folders must be ordered: active first, then removed (IsRemoved=true).
    public void SaveFolderOrder(string parentPath, IEnumerable<PhotoFolder> folders)
    {
        var all = folders.ToList();
        var sidecar = new FolderOrderSidecar
        {
            Order   = all.Where(f => !f.IsRemoved).Select(f => f.Name).ToList(),
            Removed = all.Where(f =>  f.IsRemoved).Select(f => f.Name).ToList()
        };
        File.WriteAllText(Path.Combine(parentPath, FolderOrderFile), JsonSerializer.Serialize(sidecar, JsonOptions));
    }

    // photos must be ordered: active first, then removed (IsRemoved=true).
    public void SavePhotoOrder(PhotoFolder folder, IEnumerable<PhotoItem> photos)
    {
        var all = photos.ToList();
        var caps = all
            .Where(p => !string.IsNullOrEmpty(p.Caption))
            .ToDictionary(p => p.FileName, p => p.Caption);
        var mirrored = all
            .Where(p => p.IsMirrored)
            .Select(p => p.FileName)
            .ToList();
        var sidecar = new PhotoOrderSidecar
        {
            Order    = all.Where(p => !p.IsRemoved).Select(p => p.FileName).ToList(),
            Removed  = all.Where(p =>  p.IsRemoved).Select(p => p.FileName).ToList(),
            Captions = caps.Count > 0 ? caps : null,
            Mirrored = mirrored.Count > 0 ? mirrored : null
        };
        File.WriteAllText(Path.Combine(folder.FullPath, PhotoOrderFile), JsonSerializer.Serialize(sidecar, JsonOptions));
    }
}
