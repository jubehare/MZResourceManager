using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class MzMapData
{
    [JsonPropertyName("tilesetId")] public int TilesetId { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("data")] public int[] Data { get; set; } = [];
}
