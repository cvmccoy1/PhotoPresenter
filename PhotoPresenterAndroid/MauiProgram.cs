using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;

namespace PhotoPresenterAndroid;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement();
        return builder.Build();
    }
}
