using System.Windows;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Views;

public partial class ExportProgressDialog : Window
{
    private readonly IReadOnlyList<PhotoItemViewModel> _favorites;
    private readonly string _destFolder;
    private readonly IReadOnlyList<string> _toDelete;

    public ExportProgressDialog(IReadOnlyList<PhotoItemViewModel> favorites, string destFolder,
        IReadOnlyList<string>? toDelete = null)
    {
        InitializeComponent();
        _favorites = favorites;
        _destFolder = destFolder;
        _toDelete = toDelete ?? Array.Empty<string>();
        Loaded += async (_, _) => await StartExportAsync();
    }

    private async Task StartExportAsync()
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
                string path = Path.Combine(_destFolder, fileName);
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
            string dest = Path.Combine(_destFolder, photo.FileName);
            if (!File.Exists(dest))
                await Task.Run(() => File.Copy(photo.FullPath, dest));
            done++;
            ExportProgressBar.Value = done * 100.0 / totalSteps;
            CountText.Text = $"{done} of {totalSteps}";
        }

        HeadingText.Text = "Export complete.";
        StatusText.Text = "";
        CloseButton.IsEnabled = true;
    }
}
