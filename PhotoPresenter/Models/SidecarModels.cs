using System.Text.Json.Serialization;

namespace PhotoPresenter.Models;

public class FolderOrderSidecar
{
    [JsonPropertyName("order")]   public List<string> Order   { get; set; } = new();
    [JsonPropertyName("removed")] public List<string> Removed { get; set; } = new();
}

public class PhotoOrderSidecar
{
    [JsonPropertyName("order")]   public List<string> Order   { get; set; } = new();
    [JsonPropertyName("removed")] public List<string> Removed { get; set; } = new();
}
