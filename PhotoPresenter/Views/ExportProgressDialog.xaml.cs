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

    // Filesystem destination (existing path).
    public ExportProgressDialog(IReadOnlyList<PhotoItemViewModel> favorites, string destFolder,
        IReadOnlyList<string>? toDelete = null)
    {
        InitializeComponent();
        _favorites = favorites;
        _destFolder = destFolder;
        _toDelete = toDelete ?? Array.Empty<string>();
        Loaded += async (_, _) => await StartExportAsync();
    }

    // MTP / shell-namespace destination (phone folder selected via ShellFolderPicker).
    // Delete-non-favorites is not supported for MTP destinations.
    internal ExportProgressDialog(IReadOnlyList<PhotoItemViewModel> favorites, IShellItem destShellItem)
    {
        InitializeComponent();
        _favorites = favorites;
        _destShellItem = destShellItem;
        _toDelete = Array.Empty<string>();
        Loaded += async (_, _) => await StartExportAsync();
    }

    private async Task StartExportAsync()
    {
        if (_destShellItem != null)
            await StartMtpExportAsync();
        else
            await StartFilesystemExportAsync();
    }

    // ── Filesystem export (existing behaviour, unchanged) ─────────────────────

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
        CountText.Text = $"0 of {totalSteps}";

        // Phase 1: delete non-favorites from destination
        if (_toDelete.Count > 0)
        {
            HeadingText.Text = "Deleting non-favorites…";
            foreach (var fileName in _toDelete)
            {
                StatusText.Text = fileName;
                string path = Path.Combine(_destFolder!, fileName);
                try
                {
                    if (File.Exists(path))
                        await Task.Run(() => File.Delete(path));
                }
                catch { /* skip files that can't be deleted */ }
                done++;
                ExportProgressBar.Value = done * 100.0 / totalSteps;
                CountText.Text = $"{done} of {totalSteps}";
            }
        }

        // Phase 2: copy favorites to destination
        HeadingText.Text = "Copying favorites…";
        for (int i = 0; i < _favorites.Count; i++)
        {
            var photo = _favorites[i];
            StatusText.Text = photo.FileName;
            string dest = Path.Combine(_destFolder!, photo.FileName);
            if (!File.Exists(dest))
                await Task.Run(() => File.Copy(photo.FullPath, dest));
            done++;
            ExportProgressBar.Value = done * 100.0 / totalSteps;
            CountText.Text = $"{done} of {totalSteps}";
        }

        // Phase 3: write manifest
        HeadingText.Text = "Writing manifest…";
        string json = BuildManifestJson();
        await File.WriteAllTextAsync(Path.Combine(_destFolder!, "_presentation.json"), json);

        HeadingText.Text = "Export complete.";
        StatusText.Text = "";
        CloseButton.IsEnabled = true;
    }

    // ── MTP export (shell-namespace via IFileOperation) ───────────────────────

    private async Task StartMtpExportAsync()
    {
        if (_favorites.Count == 0)
        {
            HeadingText.Text = "No favorites to export.";
            ExportProgressBar.Value = 100;
            CloseButton.IsEnabled = true;
            return;
        }

        int totalSteps = _favorites.Count + 1; // +1 for manifest
        int done = 0;
        CountText.Text = $"0 of {totalSteps}";
        HeadingText.Text = "Copying favorites to phone…";

        // Capture the COM shell item for use on the background thread.
        var dest = _destShellItem!;

        for (int i = 0; i < _favorites.Count; i++)
        {
            var photo = _favorites[i];
            StatusText.Text = photo.FileName;
            string srcPath = photo.FullPath;
            string fileName = photo.FileName;
            try
            {
                await Task.Run(() => ShellFileOperation.CopyFile(srcPath, dest, fileName));
            }
            catch { /* skip files that fail — same as File.Copy behaviour */ }
            done++;
            ExportProgressBar.Value = done * 100.0 / totalSteps;
            CountText.Text = $"{done} of {totalSteps}";
        }

        // Write manifest to the phone.
        HeadingText.Text = "Writing manifest…";
        StatusText.Text = "_presentation.json";
        string json = BuildManifestJson();
        try
        {
            await Task.Run(() => ShellFileOperation.WriteTextFile(json, dest, "_presentation.json"));
        }
        catch { /* non-fatal — presentation will still work with existing manifest */ }
        done++;
        ExportProgressBar.Value = 100;
        CountText.Text = $"{done} of {totalSteps}";

        HeadingText.Text = "Export complete.";
        StatusText.Text = "";
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
