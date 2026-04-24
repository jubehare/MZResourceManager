using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class MzActor
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("nickname")] public string Nickname { get; set; } = string.Empty;
    [JsonPropertyName("profile")] public string Profile { get; set; } = string.Empty;
    [JsonPropertyName("classId")] public int ClassId { get; set; }
    [JsonPropertyName("faceName")] public string FaceName { get; set; } = string.Empty;
    [JsonPropertyName("faceIndex")] public int FaceIndex { get; set; }
    [JsonPropertyName("characterName")] public string CharacterName { get; set; } = string.Empty;
}
