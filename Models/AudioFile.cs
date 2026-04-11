using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class AudioFile
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("volume")] public int Volume { get; set; } = 90;
    [JsonPropertyName("pitch")] public int Pitch { get; set; } = 100;
    [JsonPropertyName("pan")] public int Pan { get; set; } = 0;
}
