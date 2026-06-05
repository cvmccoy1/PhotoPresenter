using System.Windows;

namespace PhotoPresenter.Services;

public static class ThemeService
{
    private static readonly string[] ValidThemes =
        ["Light", "Dark", "HighContrastLight", "HighContrastDark", "LightLargeText", "DarkLargeText"];

    public static void Apply(string themeName)
    {
        if (!ValidThemes.Contains(themeName)) themeName = "Light";
        var dict = new ResourceDictionary
        {
            Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative)
        };
        Application.Current.Resources.MergedDictionaries[0] = dict;
    }
}
