using System.Windows;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Views;

public partial class ExportProgressDialog : Window
{
    private readonly IReadOnlyList<PhotoItemViewModel> _favorites;
    private readonly string _destFolder;

    public ExportProgressDialog(IReadOnlyList<PhotoItemViewModel> favorites, string destFolder)
    {
        InitializeComponent();
        _favorites = favorites;
        _destFolder = destFolder;
        Loaded += async (_, _) => await StartExportAsync();
    }

    private async Task StartExportAsync()
    {
        int total = _favorites.Count;

        if (total == 0)
        {
            HeadingText.Text = "No favorites to export.";
            ExportProgressBar.Value = 100;
            CloseButton.IsEnabled = true;
            return;
        }

        CountText.Text = $"0 of {total}";

        for (int i = 0; i < total; i++)
        {
            var photo = _favorites[i];
            StatusText.Text = photo.FileName;
            string dest = Path.Combine(_destFolder, photo.FileName);
            if (!File.Exists(dest))
                await Task.Run(() => File.Copy(photo.FullPath, dest));
            ExportProgressBar.Value = (i + 1) * 100.0 / total;
            CountText.Text = $"{i + 1} of {total}";
        }

        HeadingText.Text = "Export complete.";
        StatusText.Text = "";
        CloseButton.IsEnabled = true;
    }
}
