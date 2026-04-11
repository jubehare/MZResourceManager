using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class MapInfo
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("parentId")] public int ParentId { get; set; }
    public override string ToString() => $"{Id:D3} {Name}";
}
