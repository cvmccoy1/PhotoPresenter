using System.Windows;

namespace PhotoPresenter.Views;

public enum ExportDeleteChoice { DeleteAndExport, ExportOnly, Cancel }

public partial class ExportDeleteConfirmDialog : Window
{
    public ExportDeleteChoice Choice { get; private set; } = ExportDeleteChoice.Cancel;

    public ExportDeleteConfirmDialog(IReadOnlyList<string> filesToDelete)
    {
        InitializeComponent();
        HeadingText.Text = $"{filesToDelete.Count} file(s) in the destination folder are not favorites and will be deleted:";
        FileList.ItemsSource = filesToDelete;
    }

    private void DeleteAndExport_Click(object sender, RoutedEventArgs e)
    {
        Choice = ExportDeleteChoice.DeleteAndExport;
        DialogResult = true;
    }

    private void ExportOnly_Click(object sender, RoutedEventArgs e)
    {
        Choice = ExportDeleteChoice.ExportOnly;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = ExportDeleteChoice.Cancel;
        DialogResult = false;
    }
}
