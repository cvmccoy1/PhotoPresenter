using PhotoPresenterAndroid.Pages;

namespace PhotoPresenterAndroid;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(BrowsePage), typeof(BrowsePage));
        Routing.RegisterRoute(nameof(PresentPage), typeof(PresentPage));
    }
}
