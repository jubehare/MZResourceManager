using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class MzTroop
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("pages")] public List<EventPage> Pages { get; set; } = [];
}
