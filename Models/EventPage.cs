using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class EventPage
{
    [JsonPropertyName("list")] public List<EventCommand> List { get; set; } = [];
    [JsonPropertyName("image")] public EventPageImage? Image { get; set; }
}

public class EventPageImage
{
    [JsonPropertyName("characterName")] public string CharacterName { get; set; } = string.Empty;
    [JsonPropertyName("characterIndex")] public int CharacterIndex { get; set; }
    [JsonPropertyName("tileId")] public int TileId { get; set; }
}
