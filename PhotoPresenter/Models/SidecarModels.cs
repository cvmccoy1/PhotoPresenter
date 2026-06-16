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
    [JsonPropertyName("captions")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, string>? Captions { get; set; }
    [JsonPropertyName("mirrored")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<string>?               Mirrored { get; set; }
    [JsonPropertyName("favorites")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<string>?              Favorites { get; set; }
    [JsonPropertyName("adjustments")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, PhotoAdjustment>? Adjustments { get; set; }
}

public record PhotoAdjustment(int Brightness, int Contrast);
