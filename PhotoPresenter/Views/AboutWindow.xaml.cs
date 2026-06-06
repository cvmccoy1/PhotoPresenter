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
        var buildDate = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate")?.Value ?? "";
        VersionText.Text = $"Version {version?.Major}.{version?.Minor}.{version?.Build}  (built {buildDate})";

        var copyright = asm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";
        CopyrightText.Text = copyright;
    }

    private void OK_Click(object sender, RoutedEventArgs e) => Close();
}
