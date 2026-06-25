using System.Text.Json;
using PhotoPresenterAndroid.Models;

namespace PhotoPresenterAndroid.Services;

public static class ManifestService
{
    private static readonly HashSet<string> SupportedVideoExts =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".m4v" };

    private static readonly HashSet<string> AllVideoExts =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".m4v", ".avi", ".wmv", ".mkv" };

    public static async Task<List<MediaItem>> LoadAsync(string folderPath)
    {
        var jsonPath = Path.Combine(folderPath, "_presentation.json");
        if (!File.Exists(jsonPath)) return [];

        PresentationManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PresentationManifest>(
                await File.ReadAllTextAsync(jsonPath));
        }
        catch { return []; }

        if (manifest is null) return [];

        return manifest.Items
            .Where(item =>
            {
                if (!File.Exists(Path.Combine(folderPath, item.File))) return false;
                var ext = Path.GetExtension(item.File);
                // Skip video formats Android can't play; keep images and supported video.
                return !AllVideoExts.Contains(ext) || SupportedVideoExts.Contains(ext);
            })
            .Select(item => new MediaItem(
                Path.Combine(folderPath, item.File),
                item.Caption,
                SupportedVideoExts.Contains(Path.GetExtension(item.File))))
            .ToList();
    }

    public static bool HasManifest(string folderPath) =>
        File.Exists(Path.Combine(folderPath, "_presentation.json"));
}
