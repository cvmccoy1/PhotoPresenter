using System.Windows;
using PhotoPresenter.Services;

namespace PhotoPresenter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var theme = UserSettings.Load().Theme;
        if (!string.IsNullOrEmpty(theme))
            ThemeService.Apply(theme);
    }
}
