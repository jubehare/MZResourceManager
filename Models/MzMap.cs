using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class MzMap
{
    [JsonPropertyName("tilesetId")] public int TilesetId { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("battleback1Name")] public string Battleback1Name { get; set; } = string.Empty;
    [JsonPropertyName("battleback2Name")] public string Battleback2Name { get; set; } = string.Empty;
    [JsonPropertyName("parallaxName")] public string ParallaxName { get; set; } = string.Empty;
    [JsonPropertyName("bgm")] public AudioFile? Bgm { get; set; }
    [JsonPropertyName("bgs")] public AudioFile? Bgs { get; set; }
    [JsonPropertyName("autoplayBgm")] public bool AutoplayBgm { get; set; }
    [JsonPropertyName("autoplayBgs")] public bool AutoplayBgs { get; set; }
    [JsonPropertyName("events")] public MzEvent?[] Events { get; set; } = [];
}
