using PhotoPresenterAndroid.Services;
using PhotoPresenterAndroid.Tests.Infrastructure;

namespace PhotoPresenterAndroid.Tests.Unit;

public class ManifestServiceTests
{
    // ── HasManifest ──────────────────────────────────────────────────────────

    [Fact]
    public void HasManifest_ReturnsFalse_WhenManifestMissing()
    {
        using var dir = new TempDirectory();
        Assert.False(ManifestService.HasManifest(dir.Path));
    }

    [Fact]
    public void HasManifest_ReturnsTrue_WhenManifestExists()
    {
        using var dir = new TempDirectory();
        dir.CreateFile("_presentation.json", "{}"u8.ToArray());
        Assert.True(ManifestService.HasManifest(dir.Path));
    }

    // ── LoadAsync — early-exit cases ─────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_ReturnsEmpty_WhenManifestMissing()
    {
        using var dir = new TempDirectory();
        var result = await ManifestService.LoadAsync(dir.Path);
        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAsync_ReturnsEmpty_WhenJsonIsInvalid()
    {
        using var dir = new TempDirectory();
        dir.CreateFile("_presentation.json", "NOT JSON"u8.ToArray());
        var result = await ManifestService.LoadAsync(dir.Path);
        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAsync_ReturnsEmpty_WhenItemsArrayIsEmpty()
    {
        using var dir = new TempDirectory();
        WriteManifest(dir, """{"version":1,"items":[]}""");
        var result = await ManifestService.LoadAsync(dir.Path);
        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAsync_SkipsItems_WhenFileDoesNotExist()
    {
        using var dir = new TempDirectory();
        WriteManifest(dir, """{"items":[{"file":"missing.jpg"}]}""");
        // no actual file created
        var result = await ManifestService.LoadAsync(dir.Path);
        Assert.Empty(result);
    }

    // ── LoadAsync — photo items ───────────────────────────────────────────────

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.png")]
    [InlineData("photo.heic")]
    [InlineData("photo.heif")]
    [InlineData("photo.webp")]
    public async Task LoadAsync_ReturnsPhoto_ForCommonImageFormats(string fileName)
    {
        using var dir = new TempDirectory();
        dir.CreateFile(fileName);
        WriteManifest(dir, $$"""{"items":[{"file":"{{fileName}}"}]}""");

        var result = await ManifestService.LoadAsync(dir.Path);

        var item = Assert.Single(result);
        Assert.False(item.IsVideo);
        Assert.EndsWith(fileName, item.FullPath);
    }

    // ── LoadAsync — supported video items ────────────────────────────────────

    [Theory]
    [InlineData("clip.mp4")]
    [InlineData("clip.mov")]
    [InlineData("clip.m4v")]
    public async Task LoadAsync_ReturnsVideo_ForSupportedFormats(string fileName)
    {
        using var dir = new TempDirectory();
        dir.CreateFile(fileName);
        WriteManifest(dir, $$"""{"items":[{"file":"{{fileName}}"}]}""");

        var result = await ManifestService.LoadAsync(dir.Path);

        var item = Assert.Single(result);
        Assert.True(item.IsVideo);
    }

    // ── LoadAsync — unsupported video formats are filtered out ───────────────

    [Theory]
    [InlineData("clip.mkv")]
    [InlineData("clip.avi")]
    [InlineData("clip.wmv")]
    public async Task LoadAsync_Filters_UnsupportedVideoFormats(string fileName)
    {
        using var dir = new TempDirectory();
        dir.CreateFile(fileName);
        WriteManifest(dir, $$"""{"items":[{"file":"{{fileName}}"}]}""");

        var result = await ManifestService.LoadAsync(dir.Path);

        Assert.Empty(result);
    }

    // ── LoadAsync — extension matching is case-insensitive ───────────────────

    [Theory]
    [InlineData("clip.MP4")]
    [InlineData("clip.MOV")]
    [InlineData("clip.M4V")]
    [InlineData("photo.JPG")]
    [InlineData("photo.HEIC")]
    public async Task LoadAsync_ExtensionMatching_IsCaseInsensitive(string fileName)
    {
        using var dir = new TempDirectory();
        dir.CreateFile(fileName);
        WriteManifest(dir, $$"""{"items":[{"file":"{{fileName}}"}]}""");

        var result = await ManifestService.LoadAsync(dir.Path);

        Assert.Single(result);
    }

    // ── LoadAsync — FullPath ──────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_FullPath_CombinesFolderAndFileName()
    {
        using var dir = new TempDirectory();
        dir.CreateFile("photo.jpg");
        WriteManifest(dir, """{"items":[{"file":"photo.jpg"}]}""");

        var result = await ManifestService.LoadAsync(dir.Path);

        Assert.Equal(Path.Combine(dir.Path, "photo.jpg"), result[0].FullPath);
    }

    // ── LoadAsync — captions ─────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_Caption_IsNullWhenOmittedFromJson()
    {
        using var dir = new TempDirectory();
        dir.CreateFile("photo.jpg");
        WriteManifest(dir, """{"items":[{"file":"photo.jpg"}]}""");

        var result = await ManifestService.LoadAsync(dir.Path);

        Assert.Null(result[0].Caption);
    }

    [Fact]
    public async Task LoadAsync_Caption_IsPreservedWhenPresent()
    {
        using var dir = new TempDirectory();
        dir.CreateFile("photo.jpg");
        WriteManifest(dir, """{"items":[{"file":"photo.jpg","caption":"Hello world"}]}""");

        var result = await ManifestService.LoadAsync(dir.Path);

        Assert.Equal("Hello world", result[0].Caption);
    }

    [Fact]
    public async Task LoadAsync_Caption_IsNullWhenExplicitlyNull()
    {
        using var dir = new TempDirectory();
        dir.CreateFile("photo.jpg");
        WriteManifest(dir, """{"items":[{"file":"photo.jpg","caption":null}]}""");

        var result = await ManifestService.LoadAsync(dir.Path);

        Assert.Null(result[0].Caption);
    }

    // ── LoadAsync — ordering and mixed lists ─────────────────────────────────

    [Fact]
    public async Task LoadAsync_PreservesManifestOrder()
    {
        using var dir = new TempDirectory();
        dir.CreateFile("b.jpg");
        dir.CreateFile("a.jpg");
        dir.CreateFile("c.jpg");
        WriteManifest(dir, """{"items":[{"file":"b.jpg"},{"file":"a.jpg"},{"file":"c.jpg"}]}""");

        var result = await ManifestService.LoadAsync(dir.Path);

        Assert.Equal(["b.jpg", "a.jpg", "c.jpg"],
            result.Select(r => Path.GetFileName(r.FullPath)));
    }

    [Fact]
    public async Task LoadAsync_MixedList_SupportedItemsOnly()
    {
        using var dir = new TempDirectory();
        dir.CreateFile("photo.jpg");
        dir.CreateFile("video.mp4");
        dir.CreateFile("unsupported.mkv");
        // "missing.jpg" intentionally not created — ManifestService must skip it
        WriteManifest(dir, """
            {"items":[
                {"file":"photo.jpg","caption":"A photo"},
                {"file":"video.mp4"},
                {"file":"unsupported.mkv"},
                {"file":"missing.jpg"}
            ]}
            """);

        var result = await ManifestService.LoadAsync(dir.Path);

        Assert.Equal(2, result.Count);
        Assert.False(result[0].IsVideo);
        Assert.Equal("A photo", result[0].Caption);
        Assert.True(result[1].IsVideo);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void WriteManifest(TempDirectory dir, string json) =>
        dir.CreateFile("_presentation.json", System.Text.Encoding.UTF8.GetBytes(json));
}
