using System.IO;
using System.Text.Json;
using PhotoPresenter.Models;

namespace PhotoPresenter.Services;

public class PhotoLibraryService : IPhotoLibraryService
{
    private static readonly HashSet<string> PhotoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif" };

    private const string FolderOrderFile = "_photofolderorder.json";
    private const string PhotoOrderFile = "_photoorder.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<List<PhotoFolder>> LoadLibraryAsync(string parentPath)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(parentPath))
                return new List<PhotoFolder>();

            var subdirs = Directory.GetDirectories(parentPath)
                .Select(d => new DirectoryInfo(d))
                .ToList();

            var orderedDirs = ApplyFolderOrder(parentPath, subdirs);
            return orderedDirs.Select(dir => new PhotoFolder
            {
                Name = dir.Name,
                FullPath = dir.FullName,
                Photos = LoadPhotosForFolder(dir.FullName)
            }).ToList();
        });
    }

    private static List<DirectoryInfo> ApplyFolderOrder(string parentPath, List<DirectoryInfo> subdirs)
    {
        var sidecarPath = Path.Combine(parentPath, FolderOrderFile);
        if (!File.Exists(sidecarPath))
            return subdirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();

        try
        {
            var sidecar = JsonSerializer.Deserialize<FolderOrderSidecar>(File.ReadAllText(sidecarPath));
            if (sidecar?.Order == null)
                return subdirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();

            var lookup = subdirs.ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);
            var ordered = new List<DirectoryInfo>();

            foreach (var name in sidecar.Order)
                if (lookup.Remove(name, out var dir))
                    ordered.Add(dir);

            ordered.AddRange(lookup.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase));
            return ordered;
        }
        catch
        {
            return subdirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    private static List<PhotoItem> LoadPhotosForFolder(string folderPath)
    {
        var files = Directory.GetFiles(folderPath)
            .Where(f => PhotoExtensions.Contains(Path.GetExtension(f)))
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

            var lookup = files.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
            var ordered = new List<FileInfo>();

            foreach (var name in sidecar.Order)
                if (lookup.Remove(name, out var file))
                    ordered.Add(file);

            ordered.AddRange(lookup.Values.OrderBy(f => f.CreationTime));
            return ordered.Select(ToPhotoItem).ToList();
        }
        catch
        {
            return files.OrderBy(f => f.CreationTime).Select(ToPhotoItem).ToList();
        }
    }

    private static PhotoItem ToPhotoItem(FileInfo file) => new()
    {
        FileName = file.Name,
        FullPath = file.FullName,
        CreationDate = file.CreationTime
    };

    public void SaveFolderOrder(string parentPath, IEnumerable<PhotoFolder> folders)
    {
        var sidecar = new FolderOrderSidecar { Order = folders.Select(f => f.Name).ToList() };
        File.WriteAllText(Path.Combine(parentPath, FolderOrderFile), JsonSerializer.Serialize(sidecar, JsonOptions));
    }

    public void SavePhotoOrder(PhotoFolder folder, IEnumerable<PhotoItem> photos)
    {
        var sidecar = new PhotoOrderSidecar { Order = photos.Select(p => p.FileName).ToList() };
        File.WriteAllText(Path.Combine(folder.FullPath, PhotoOrderFile), JsonSerializer.Serialize(sidecar, JsonOptions));
    }
}
