using System.Text.Json;
using System.Windows;
using PhotoPresenter.Models;
using PhotoPresenter.Services;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Views;

public partial class ExportProgressDialog : Window
{
    private readonly IReadOnlyList<PhotoItemViewModel> _favorites;
    private readonly string? _destFolder;          // non-null for filesystem destinations
    private readonly IShellItem? _destShellItem;   // non-null for MTP destinations
    private readonly IReadOnlyList<string> _toDelete;

    // Filesystem destination — mirror copy: delete _toDelete, copy/update favorites by size.
    public ExportProgressDialog(IReadOnlyList<PhotoItemViewModel> favorites, string destFolder,
        IReadOnlyList<string>? toDelete = null)
    {
        InitializeComponent();
        _favorites = favorites;
        _destFolder = destFolder;
        _toDelete = toDelete ?? Array.Empty<string>();
        Loaded += async (_, _) => await StartExportAsync();
    }

    // MTP / shell-namespace destination — full mirror: scan phone, delete stale files,
    // copy new/changed favorites, skip unchanged. No delete confirmation (scanning happens
    // inside the progress dialog so the main window stays responsive).
    internal ExportProgressDialog(IReadOnlyList<PhotoItemViewModel> favorites, IShellItem destShellItem)
    {
        InitializeComponent();
        _favorites = favorites;
        _destShellItem = destShellItem;
        _toDelete = Array.Empty<string>();
        Loaded += async (_, _) => await StartExportAsync();
    }

    private Task StartExportAsync() =>
        _destShellItem != null ? StartMtpExportAsync() : StartFilesystemExportAsync();

    // ── Filesystem export ──────────────────────────────────────────────────────
    // Mirror semantics: delete _toDelete (caller computed, user confirmed), then
    // copy/overwrite favorites whose destination is missing or has a different size.

    private async Task StartFilesystemExportAsync()
    {
        int totalSteps = _toDelete.Count + _favorites.Count;
        if (totalSteps == 0)
        {
            HeadingText.Text = "No favorites to export.";
            ExportProgressBar.Value = 100;
            CloseButton.IsEnabled = true;
            return;
        }

        int done = 0;
        int copied = 0;
        CountText.Text = $"0 of {totalSteps}";

        // Phase 1: delete non-favorites (only when user chose DeleteAndExport)
        if (_toDelete.Count > 0)
        {
            HeadingText.Text = "Deleting non-favorites…";
            foreach (var fileName in _toDelete)
            {
                StatusText.Text = fileName;
                string path = Path.Combine(_destFolder!, fileName);
                try { if (File.Exists(path)) await Task.Run(() => File.Delete(path)); }
                catch { /* skip files that can't be deleted */ }
                done++;
                ExportProgressBar.Value = done * 100.0 / totalSteps;
                CountText.Text = $"{done} of {totalSteps}";
            }
        }

        // Phase 2: mirror-copy favorites (skip if destination exists with same size)
        HeadingText.Text = "Syncing favorites…";
        for (int i = 0; i < _favorites.Count; i++)
        {
            var photo = _favorites[i];
            StatusText.Text = photo.FileName;
            string dest = Path.Combine(_destFolder!, photo.FileName);

            bool needsCopy;
            try
            {
                var srcLen = new FileInfo(photo.FullPath).Length;
                var dstInfo = new FileInfo(dest);
                needsCopy = !dstInfo.Exists || dstInfo.Length != srcLen;
            }
            catch { needsCopy = true; }

            if (needsCopy)
            {
                try { await Task.Run(() => File.Copy(photo.FullPath, dest, overwrite: true)); }
                catch { /* skip on error */ }
                copied++;
            }

            done++;
            ExportProgressBar.Value = done * 100.0 / totalSteps;
            CountText.Text = $"{done} of {totalSteps}";
        }

        // Phase 3: write manifest
        HeadingText.Text = "Writing manifest…";
        string json = BuildManifestJson();
        await File.WriteAllTextAsync(Path.Combine(_destFolder!, "_presentation.json"), json);

        int skipped = _favorites.Count - copied;
        HeadingText.Text = "Export complete.";
        StatusText.Text = skipped > 0 ? $"{skipped} file(s) already up to date, skipped." : "";
        CloseButton.IsEnabled = true;
    }

    // ── MTP export ─────────────────────────────────────────────────────────────
    // Full mirror: scan destination, delete non-favorites, copy new/changed files,
    // skip files that are already present with the same byte size.

    private async Task StartMtpExportAsync()
    {
        if (_favorites.Count == 0)
        {
            HeadingText.Text = "No favorites to export.";
            ExportProgressBar.Value = 100;
            CloseButton.IsEnabled = true;
            return;
        }

        var dest = _destShellItem!;

        // Phase 0: scan the phone folder
        HeadingText.Text = "Scanning phone folder…";
        StatusText.Text = "";
        CountText.Text = "";

        Dictionary<string, (ulong Size, IShellItem Item)> existing = [];
        try { existing = await Task.Run(() => ShellFileOperation.EnumerateFolderContents(dest)); }
        catch { /* proceed as if destination is empty */ }

        // Compute what needs to change
        var favoriteNames = new HashSet<string>(
            _favorites.Select(f => f.FileName), StringComparer.OrdinalIgnoreCase);

        var toDelete = existing
            .Where(kv => !favoriteNames.Contains(kv.Key))
            .Select(kv => (kv.Key, kv.Value.Item))
            .ToList();

        var toCopy = _favorites.Where(f =>
        {
            if (!existing.TryGetValue(f.FileName, out var ex)) return true; // new file
            try { return (ulong)new FileInfo(f.FullPath).Length != ex.Size; } // changed?
            catch { return true; }
        }).ToList();

        int totalSteps = toDelete.Count + toCopy.Count + 1; // +1 for manifest
        int done = 0;
        CountText.Text = $"0 of {totalSteps}";

        // Phase 1: delete non-favorites from phone
        if (toDelete.Count > 0)
        {
            HeadingText.Text = "Deleting removed files from phone…";
            foreach (var (name, item) in toDelete)
            {
                StatusText.Text = name;
                try { await Task.Run(() => ShellFileOperation.DeleteShellItem(item)); }
                catch { /* non-fatal */ }
                done++;
                ExportProgressBar.Value = done * 100.0 / totalSteps;
                CountText.Text = $"{done} of {totalSteps}";
            }
        }

        // Phase 2: copy new and changed files to phone
        HeadingText.Text = "Copying favorites to phone…";
        foreach (var photo in toCopy)
        {
            StatusText.Text = photo.FileName;
            try { await Task.Run(() => ShellFileOperation.CopyFile(photo.FullPath, dest, photo.FileName)); }
            catch { /* skip files that fail */ }
            done++;
            ExportProgressBar.Value = done * 100.0 / totalSteps;
            CountText.Text = $"{done} of {totalSteps}";
        }

        // Phase 3: write manifest
        HeadingText.Text = "Writing manifest…";
        StatusText.Text = "_presentation.json";
        string json = BuildManifestJson();
        try { await Task.Run(() => ShellFileOperation.WriteTextFile(json, dest, "_presentation.json")); }
        catch { /* non-fatal */ }
        done++;
        ExportProgressBar.Value = 100;
        CountText.Text = $"{done} of {totalSteps}";

        int skipped = _favorites.Count - toCopy.Count;
        HeadingText.Text = "Export complete.";
        StatusText.Text = skipped > 0 ? $"{skipped} file(s) already up to date, skipped." : "";
        CloseButton.IsEnabled = true;
    }

    // ── Shared ────────────────────────────────────────────────────────────────

    private string BuildManifestJson()
    {
        var manifest = new PresentationManifest
        {
            Items = _favorites
                .Select(f => new PresentationManifestItem(
                    f.FileName,
                    string.IsNullOrEmpty(f.Caption) ? null : f.Caption))
                .ToList()
        };
        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
    }
}
