using System.Text.Json;

namespace PhotoPresenter.Services;

public class UserSettings
{
    public string LastParentFolder { get; set; } = "";
    public string LastSelectedFolder { get; set; } = "";
    public string LastSelectedPhoto { get; set; } = "";
    public double PhotoScrollOffset { get; set; } = 0;
    public double FolderScrollOffset { get; set; } = 0;
    public bool ShowAllFolders { get; set; } = false;
    public bool ShowAllPhotos { get; set; } = false;
    public double Volume { get; set; } = 0.5;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
    public double? SplitterPosition { get; set; }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoPresenter", "settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this));
        }
        catch { }
    }
}
