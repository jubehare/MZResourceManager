using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class MzCommonEvent
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("list")] public List<EventCommand> List { get; set; } = [];
}
