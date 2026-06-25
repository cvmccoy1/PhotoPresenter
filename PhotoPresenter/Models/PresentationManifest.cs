using System.Text.Json.Serialization;

namespace PhotoPresenter.Models;

public record PresentationManifestItem(
    [property: JsonPropertyName("file")]
    string File,
    [property: JsonPropertyName("caption")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Caption = null);

public class PresentationManifest
{
    [JsonPropertyName("version")] public int Version { get; init; } = 1;
    [JsonPropertyName("items")]   public List<PresentationManifestItem> Items { get; init; } = [];
}
