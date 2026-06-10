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
    public string Theme { get; set; } = "Light";
    public string TextSize { get; set; } = "Normal";

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoPresenter", "settings.json");

    public static UserSettings Load() => Load(SettingsPath);

    internal static UserSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save() => Save(SettingsPath);

    internal void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this));
        }
        catch { }
    }
}
