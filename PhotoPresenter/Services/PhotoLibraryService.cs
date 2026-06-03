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
                    Name   = dir.Name,
                    FullPath = dir.FullName,
                    Photos = LoadPhotosForFolder(dir.FullName)
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
    private static (List<DirectoryInfo> Active, List<string> RemovedNames) ApplyFolderOrder(
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
        catch
        {
            return (subdirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList(), new());
        }
    }

    // Returns active photos first (in sidecar order), removed photos at the end.
    private static List<PhotoItem> LoadPhotosForFolder(string folderPath)
    {
        var files = Directory.GetFiles(folderPath)
            .Where(f => MediaExtensions.Contains(Path.GetExtension(f)))
            .Select(f => new FileInfo(f))
            .ToList();

        var sidecarPath = Path.Combine(folderPath, PhotoOrderFile);
        if (!File.Exists(sidecarPath))
            return files.OrderBy(f => f.CreationTime).Select(ToPhotoItem).ToList();

        try
        {
            var sidecar = JsonSerializer.Deserialize<PhotoOrderSidecar>(File.ReadAllText(sidecarPath));
            if (sidecar?.Order == null)
                return files.OrderBy(f => f.CreationTime).Select(ToPhotoItem).ToList();

            var removedNames = sidecar.Removed ?? new();
            var removedSet   = new HashSet<string>(removedNames, StringComparer.OrdinalIgnoreCase);
            var allFiles     = files.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
            var mutable      = new Dictionary<string, FileInfo>(allFiles, StringComparer.OrdinalIgnoreCase);
            var result       = new List<PhotoItem>();

            // Active in sidecar order
            foreach (var name in sidecar.Order)
                if (mutable.Remove(name, out var file))
                    result.Add(ToPhotoItem(file));

            // New files (not in order, not in removed) appended by creation date
            foreach (var file in mutable.Values
                .Where(f => !removedSet.Contains(f.Name))
                .OrderBy(f => f.CreationTime))
                result.Add(ToPhotoItem(file));

            // Removed files that still exist on disk
            foreach (var name in removedNames)
                if (allFiles.TryGetValue(name, out var file))
                    result.Add(new PhotoItem
                    {
                        FileName     = file.Name,
                        FullPath     = file.FullName,
                        CreationDate = file.CreationTime,
                        IsRemoved    = true,
                        IsVideo      = VideoExtensions.Contains(Path.GetExtension(file.Name))
                    });

            return result;
        }
        catch
        {
            return files.OrderBy(f => f.CreationTime).Select(ToPhotoItem).ToList();
        }
    }

    private static PhotoItem ToPhotoItem(FileInfo file) => new()
    {
        FileName     = file.Name,
        FullPath     = file.FullName,
        CreationDate = file.CreationTime,
        IsVideo      = VideoExtensions.Contains(Path.GetExtension(file.Name))
    };

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
        var sidecar = new PhotoOrderSidecar
        {
            Order   = all.Where(p => !p.IsRemoved).Select(p => p.FileName).ToList(),
            Removed = all.Where(p =>  p.IsRemoved).Select(p => p.FileName).ToList()
        };
        File.WriteAllText(Path.Combine(folder.FullPath, PhotoOrderFile), JsonSerializer.Serialize(sidecar, JsonOptions));
    }
}
