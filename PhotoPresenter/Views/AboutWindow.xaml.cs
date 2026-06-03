using System.Reflection;
using System.Windows;

namespace PhotoPresenter.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var asm = Assembly.GetExecutingAssembly();
        var version = asm.GetName().Version;
        VersionText.Text = $"Version {version?.Major}.{version?.Minor}.{version?.Build}";

        var copyright = asm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";
        CopyrightText.Text = copyright;
    }

    private void OK_Click(object sender, RoutedEventArgs e) => Close();
}
