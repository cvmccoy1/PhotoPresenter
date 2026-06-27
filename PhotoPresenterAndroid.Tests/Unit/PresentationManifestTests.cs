using System.Text.Json;
using PhotoPresenterAndroid.Models;

namespace PhotoPresenterAndroid.Tests.Unit;

public class PresentationManifestTests
{
    [Fact]
    public void Deserialize_EmptyItemsArray_WhenFieldAbsent()
    {
        var manifest = JsonSerializer.Deserialize<PresentationManifest>("{}")!;
        Assert.Empty(manifest.Items);
    }

    [Fact]
    public void Deserialize_EmptyItemsArray_WhenExplicitlyEmpty()
    {
        var manifest = JsonSerializer.Deserialize<PresentationManifest>("""{"items":[]}""")!;
        Assert.Empty(manifest.Items);
    }

    [Fact]
    public void Deserialize_CaptionIsNull_WhenFieldAbsent()
    {
        var manifest = JsonSerializer.Deserialize<PresentationManifest>(
            """{"items":[{"file":"img.jpg"}]}""")!;
        Assert.Null(manifest.Items[0].Caption);
    }

    [Fact]
    public void Deserialize_CaptionIsPreserved_WhenPresent()
    {
        var manifest = JsonSerializer.Deserialize<PresentationManifest>(
            """{"items":[{"file":"img.jpg","caption":"My caption"}]}""")!;
        Assert.Equal("My caption", manifest.Items[0].Caption);
    }

    [Fact]
    public void Deserialize_FileIsPreserved()
    {
        var manifest = JsonSerializer.Deserialize<PresentationManifest>(
            """{"items":[{"file":"IMG_001.jpg"}]}""")!;
        Assert.Equal("IMG_001.jpg", manifest.Items[0].File);
    }

    [Fact]
    public void Deserialize_VersionField_DefaultsToOne()
    {
        var manifest = JsonSerializer.Deserialize<PresentationManifest>("{}")!;
        Assert.Equal(1, manifest.Version);
    }

    [Fact]
    public void Deserialize_VersionField_IsReadFromJson()
    {
        var manifest = JsonSerializer.Deserialize<PresentationManifest>("""{"version":2}""")!;
        Assert.Equal(2, manifest.Version);
    }

    [Fact]
    public void Serialize_OmitsCaption_WhenNull()
    {
        var item = new PresentationManifestItem("img.jpg", null);
        var json = JsonSerializer.Serialize(item);
        Assert.DoesNotContain("caption", json);
    }

    [Fact]
    public void Serialize_IncludesCaption_WhenPresent()
    {
        var item = new PresentationManifestItem("img.jpg", "Hello");
        var json = JsonSerializer.Serialize(item);
        Assert.Contains("\"caption\":\"Hello\"", json);
    }
}
