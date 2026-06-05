using System.Windows;

namespace PhotoPresenter.Services;

public static class ThemeService
{
    private static readonly string[] ValidColorThemes =
        ["Light", "Dark", "HighContrastLight", "HighContrastDark",
         "SlateBlue", "Forest", "Sunset", "Amethyst", "Teal"];

    private static readonly string[] ValidTextSizes =
        ["Small", "Normal", "Large", "XLarge"];

    public static void ApplyColor(string themeName)
    {
        if (!ValidColorThemes.Contains(themeName)) themeName = "Light";
        var dict = new ResourceDictionary
        {
            Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative)
        };
        Application.Current.Resources.MergedDictionaries[0] = dict;
    }

    public static void ApplyTextSize(string textSize)
    {
        if (!ValidTextSizes.Contains(textSize)) textSize = "Normal";
        var dict = new ResourceDictionary
        {
            Source = new Uri($"Themes/TextSize_{textSize}.xaml", UriKind.Relative)
        };
        Application.Current.Resources.MergedDictionaries[1] = dict;
    }
}
