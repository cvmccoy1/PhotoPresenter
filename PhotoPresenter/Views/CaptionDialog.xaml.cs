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
        Caption = NormalizeCaption(CaptionBox.Text);
        DialogResult = true;
    }

    private void CaptionBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Enter alone = OK; Shift+Enter falls through to AcceptsReturn (inserts newline).
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.None)
        {
            Caption = NormalizeCaption(CaptionBox.Text);
            DialogResult = true;
            e.Handled = true;
        }
    }

    private static string NormalizeCaption(string text) =>
        text.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
}
