using System.Windows;
using System.Windows.Input;

namespace PhotoPresenter.Views;

public partial class CaptionDialog : Window
{
    public string Caption { get; private set; } = "";

    public CaptionDialog(string existing = "")
    {
        InitializeComponent();
        CaptionBox.Text = existing;
        Loaded += (_, _) => { CaptionBox.Focus(); CaptionBox.SelectAll(); };
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Caption = CaptionBox.Text.Trim();
        DialogResult = true;
    }

    private void CaptionBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Caption = CaptionBox.Text.Trim();
            DialogResult = true;
            e.Handled = true;
        }
    }
}
