using PhotoPresenterAndroid.Pages;

namespace PhotoPresenterAndroid;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(PresentPage), typeof(PresentPage));
    }
}
