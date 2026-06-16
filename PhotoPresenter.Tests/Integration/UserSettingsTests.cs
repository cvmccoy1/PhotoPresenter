using PhotoPresenter.Services;
using PhotoPresenter.Tests.Infrastructure;

namespace PhotoPresenter.Tests.Integration;

public class UserSettingsTests : IDisposable
{
    private readonly TempDirectory _tmp = new();
    private string SettingsFile => System.IO.Path.Combine(_tmp.Path, "settings.json");

    public void Dispose() => _tmp.Dispose();

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var settings = UserSettings.Load(SettingsFile);

        Assert.Equal("", settings.LastParentFolder);
        Assert.Equal("Light", settings.Theme);
        Assert.Equal("Normal", settings.TextSize);
        Assert.Equal(0.5, settings.Volume);
        Assert.False(settings.ShowAllFolders);
        Assert.False(settings.ShowAllPhotos);
        Assert.True(settings.FadeTransitionEnabled);
    }

    [Fact]
    public void Load_ValidJson_AllPropertiesDeserialized()
    {
        File.WriteAllText(SettingsFile, """
            {
              "LastParentFolder": "C:\\Photos",
              "LastSelectedFolder": "Vacation",
              "LastSelectedPhoto": "img001.jpg",
              "PhotoScrollOffset": 42.5,
              "FolderScrollOffset": 10.0,
              "ShowAllFolders": true,
              "ShowAllPhotos": true,
              "Volume": 0.75,
              "WindowLeft": 100,
              "WindowTop": 200,
              "WindowWidth": 1200,
              "WindowHeight": 800,
              "WindowMaximized": true,
              "SplitterPosition": 250,
              "Theme": "Dark",
              "TextSize": "Large"
            }
            """);

        var settings = UserSettings.Load(SettingsFile);

        Assert.Equal("C:\\Photos", settings.LastParentFolder);
        Assert.Equal("Vacation", settings.LastSelectedFolder);
        Assert.Equal("img001.jpg", settings.LastSelectedPhoto);
        Assert.Equal(42.5, settings.PhotoScrollOffset);
        Assert.Equal(10.0, settings.FolderScrollOffset);
        Assert.True(settings.ShowAllFolders);
        Assert.True(settings.ShowAllPhotos);
        Assert.Equal(0.75, settings.Volume);
        Assert.Equal(100.0, settings.WindowLeft);
        Assert.Equal(200.0, settings.WindowTop);
        Assert.Equal(1200.0, settings.WindowWidth);
        Assert.Equal(800.0, settings.WindowHeight);
        Assert.True(settings.WindowMaximized);
        Assert.Equal(250.0, settings.SplitterPosition);
        Assert.Equal("Dark", settings.Theme);
        Assert.Equal("Large", settings.TextSize);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        File.WriteAllText(SettingsFile, "{ NOT VALID JSON ]]]");

        var settings = UserSettings.Load(SettingsFile);

        Assert.Equal("", settings.LastParentFolder);
        Assert.Equal("Light", settings.Theme);
    }

    [Fact]
    public void Save_ThenLoad_AllPropertiesRoundTripped()
    {
        var original = new UserSettings
        {
            LastParentFolder = @"C:\Photos",
            LastSelectedFolder = "Trip",
            LastSelectedPhoto = "img.jpg",
            PhotoScrollOffset = 55.0,
            FolderScrollOffset = 12.0,
            ShowAllFolders = true,
            ShowAllPhotos = false,
            Volume = 0.3,
            WindowLeft = 50,
            WindowTop = 60,
            WindowWidth = 900,
            WindowHeight = 700,
            WindowMaximized = false,
            SplitterPosition = 300,
            Theme = "SlateBlue",
            TextSize = "Small",
            FadeTransitionEnabled = false
        };

        original.Save(SettingsFile);
        var loaded = UserSettings.Load(SettingsFile);

        Assert.Equal(original.LastParentFolder,    loaded.LastParentFolder);
        Assert.Equal(original.LastSelectedFolder,  loaded.LastSelectedFolder);
        Assert.Equal(original.LastSelectedPhoto,   loaded.LastSelectedPhoto);
        Assert.Equal(original.PhotoScrollOffset,   loaded.PhotoScrollOffset);
        Assert.Equal(original.FolderScrollOffset,  loaded.FolderScrollOffset);
        Assert.Equal(original.ShowAllFolders,      loaded.ShowAllFolders);
        Assert.Equal(original.ShowAllPhotos,       loaded.ShowAllPhotos);
        Assert.Equal(original.Volume,              loaded.Volume);
        Assert.Equal(original.WindowLeft,          loaded.WindowLeft);
        Assert.Equal(original.WindowTop,           loaded.WindowTop);
        Assert.Equal(original.WindowWidth,         loaded.WindowWidth);
        Assert.Equal(original.WindowHeight,        loaded.WindowHeight);
        Assert.Equal(original.WindowMaximized,     loaded.WindowMaximized);
        Assert.Equal(original.SplitterPosition,    loaded.SplitterPosition);
        Assert.Equal(original.Theme,               loaded.Theme);
        Assert.Equal(original.TextSize,            loaded.TextSize);
        Assert.Equal(original.FadeTransitionEnabled, loaded.FadeTransitionEnabled);
    }

    [Fact]
    public void Save_PublicOverload_DoesNotThrow()
    {
        // The public Save() overload (line 41) delegates to Save(SettingsPath).
        // Load first so any existing window bounds / LastParentFolder are preserved —
        // saving blank defaults would regress coverage of MainWindow.RestoreWindowBounds.
        var settings = UserSettings.Load();
        Assert.Null(Record.Exception(() => settings.Save()));
    }
}
