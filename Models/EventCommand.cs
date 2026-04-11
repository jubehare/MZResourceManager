using System.Text.Json;
using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class EventCommand
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("indent")] public int Indent { get; set; }
    [JsonPropertyName("parameters")] public JsonElement[] Parameters { get; set; } = [];

    public int GetIntParam(int i) => i < Parameters.Length ? Parameters[i].GetInt32() : 0;
    public string GetStringParam(int i) => i < Parameters.Length ? Parameters[i].GetString() ?? string.Empty : string.Empty;
    public bool GetBoolParam(int i) => i < Parameters.Length && Parameters[i].GetBoolean();
    public AudioFile? GetAudioParam(int i) => i < Parameters.Length
        ? JsonSerializer.Deserialize<AudioFile>(Parameters[i].GetRawText())
        : null;
}
