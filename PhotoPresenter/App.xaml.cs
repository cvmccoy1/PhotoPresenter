using System.Windows;
using PhotoPresenter.Services;

namespace PhotoPresenter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = UserSettings.Load();
        ThemeService.ApplyColor(settings.Theme);
        ThemeService.ApplyTextSize(settings.TextSize);
    }
}
