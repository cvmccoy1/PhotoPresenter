using System.Text.Json.Serialization;

namespace PhotoPresenter.Models;

public class FolderOrderSidecar
{
    [JsonPropertyName("order")]   public List<string> Order   { get; set; } = new();
    [JsonPropertyName("removed")] public List<string> Removed { get; set; } = new();
}

public class PhotoOrderSidecar
{
    [JsonPropertyName("order")]    public List<string>                Order    { get; set; } = new();
    [JsonPropertyName("removed")]  public List<string>                Removed  { get; set; } = new();
    [JsonPropertyName("captions")] public Dictionary<string, string>? Captions { get; set; }
    [JsonPropertyName("mirrored")] public List<string>?               Mirrored { get; set; }
}
