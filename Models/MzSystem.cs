using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class MzSystem
{
    [JsonPropertyName("gameTitle")] public string GameTitle { get; set; } = string.Empty;
    [JsonPropertyName("switches")] public string[] Switches { get; set; } = [];
    [JsonPropertyName("variables")] public string[] Variables { get; set; } = [];
    [JsonPropertyName("title1Name")] public string Title1Name { get; set; } = string.Empty;
    [JsonPropertyName("title2Name")] public string Title2Name { get; set; } = string.Empty;
    [JsonPropertyName("battleback1Name")] public string Battleback1Name { get; set; } = string.Empty;
    [JsonPropertyName("battleback2Name")] public string Battleback2Name { get; set; } = string.Empty;
    [JsonPropertyName("battlerName")] public string BattlerName { get; set; } = string.Empty;
    [JsonPropertyName("titleBgm")] public AudioFile? TitleBgm { get; set; }
    [JsonPropertyName("battleBgm")] public AudioFile? BattleBgm { get; set; }
    [JsonPropertyName("defeatMe")] public AudioFile? DefeatMe { get; set; }
    [JsonPropertyName("victoryMe")] public AudioFile? VictoryMe { get; set; }
}
