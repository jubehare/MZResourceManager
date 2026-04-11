using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class MzTileset
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("note")] public string Note { get; set; } = string.Empty;
    [JsonPropertyName("tilesetNames")] public string[] TilesetNames { get; set; } = new string[9];
    public override string ToString() => $"{Id:D4} {Name}";
}
