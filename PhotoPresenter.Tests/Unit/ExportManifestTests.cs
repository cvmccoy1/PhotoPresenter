using System.Text.Json;
using PhotoPresenter.Models;

namespace PhotoPresenter.Tests.Unit;

public class ExportManifestTests
{
    // ── serialization ────────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_ItemWithCaption_IncludesCaptionKey()
    {
        var manifest = new PresentationManifest
        {
            Items = [new PresentationManifestItem("img001.jpg", "Day at the beach")]
        };
        string json = JsonSerializer.Serialize(manifest);
        Assert.Contains("\"caption\"", json);
        Assert.Contains("Day at the beach", json);
    }

    [Fact]
    public void Serialize_ItemWithNullCaption_OmitsCaptionKey()
    {
        var manifest = new PresentationManifest
        {
            Items = [new PresentationManifestItem("img001.jpg", null)]
        };
        string json = JsonSerializer.Serialize(manifest);
        Assert.DoesNotContain("\"caption\"", json);
    }

    [Fact]
    public void Serialize_ItemWithEmptyCaptionPassedAsNull_OmitsCaptionKey()
    {
        // The export code converts empty-string caption to null before creating the record.
        string? caption = string.IsNullOrEmpty("") ? null : "";
        var manifest = new PresentationManifest
        {
            Items = [new PresentationManifestItem("img001.jpg", caption)]
        };
        string json = JsonSerializer.Serialize(manifest);
        Assert.DoesNotContain("\"caption\"", json);
    }

    [Fact]
    public void Serialize_ContainsVersionAndItems()
    {
        var manifest = new PresentationManifest
        {
            Items = [new PresentationManifestItem("a.jpg")]
        };
        string json = JsonSerializer.Serialize(manifest);
        Assert.Contains("\"version\"", json);
        Assert.Contains("\"items\"", json);
    }

    [Fact]
    public void Serialize_VersionDefaultsToOne()
    {
        var manifest = new PresentationManifest();
        string json = JsonSerializer.Serialize(manifest);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public void Serialize_FileFieldUsesLowercaseJsonName()
    {
        var manifest = new PresentationManifest
        {
            Items = [new PresentationManifestItem("photo.jpg")]
        };
        string json = JsonSerializer.Serialize(manifest);
        using var doc = JsonDocument.Parse(json);
        var item = doc.RootElement.GetProperty("items")[0];
        Assert.Equal("photo.jpg", item.GetProperty("file").GetString());
    }

    // ── round-trip ───────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_MultipleItems_PreservesOrderAndCaptions()
    {
        var original = new PresentationManifest
        {
            Items =
            [
                new PresentationManifestItem("first.jpg",  "Caption one"),
                new PresentationManifestItem("second.mp4", null),
                new PresentationManifestItem("third.png",  "Line 1\nLine 2"),
            ]
        };
        string json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<PresentationManifest>(json)!;

        Assert.Equal(3, restored.Items.Count);
        Assert.Equal("first.jpg",    restored.Items[0].File);
        Assert.Equal("Caption one",  restored.Items[0].Caption);
        Assert.Equal("second.mp4",   restored.Items[1].File);
        Assert.Null(restored.Items[1].Caption);
        Assert.Equal("third.png",    restored.Items[2].File);
        Assert.Equal("Line 1\nLine 2", restored.Items[2].Caption);
    }

    [Fact]
    public void RoundTrip_EmptyItems_PreservesEmptyList()
    {
        var original = new PresentationManifest { Items = [] };
        string json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<PresentationManifest>(json)!;
        Assert.Empty(restored.Items);
    }

    [Fact]
    public void Deserialize_MissingCaptionField_ReturnsNullCaption()
    {
        // Simulate JSON produced for a caption-less item (no "caption" key).
        const string json = """{"version":1,"items":[{"file":"img.jpg"}]}""";
        var manifest = JsonSerializer.Deserialize<PresentationManifest>(json)!;
        Assert.Single(manifest.Items);
        Assert.Equal("img.jpg", manifest.Items[0].File);
        Assert.Null(manifest.Items[0].Caption);
    }
}
