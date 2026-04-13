using System.Text.Json.Serialization;

namespace MZResourceManager.Models;

public class MzItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("note")] public string Note { get; set; } = string.Empty;
    [JsonPropertyName("iconIndex")] public int IconIndex { get; set; }
    [JsonPropertyName("price")] public int Price { get; set; }
    [JsonPropertyName("itypeId")] public int ITypeId { get; set; }
    [JsonPropertyName("consumable")] public bool Consumable { get; set; }
    [JsonPropertyName("scope")] public int Scope { get; set; }
    [JsonPropertyName("occasion")] public int Occasion { get; set; }
    [JsonPropertyName("speed")] public int Speed { get; set; }
    [JsonPropertyName("successRate")] public int SuccessRate { get; set; }
    [JsonPropertyName("repeats")] public int Repeats { get; set; }

    public string TypeLabel => ITypeId switch
    {
        1 => "Regular Item",
        2 => "Key Item",
        3 => "Hidden A",
        4 => "Hidden B",
        _ => $"Type {ITypeId}",
    };

    public string OccasionLabel => Occasion switch
    {
        0 => "Always",
        1 => "Battle",
        2 => "Field",
        3 => "Never",
        _ => $"Occasion {Occasion}",
    };

    public string ScopeLabel => Scope switch
    {
        0 => "None",
        1 => "One Enemy",
        2 => "All Enemies",
        3 => "One Random Enemy",
        4 => "2 Random Enemies",
        5 => "3 Random Enemies",
        6 => "4 Random Enemies",
        7 => "One Ally",
        8 => "All Allies",
        9 => "One Ally (KO)",
        10 => "All Allies (KO)",
        11 => "User",
        12 => "One Ally (Friend)",
        13 => "All Allies (Friend)",
        14 => "Everyone",
        _ => $"Scope {Scope}",
    };

    public string ConsumableLabel => Consumable ? "Yes" : "No";

    public NamedEntry ToNamedEntry() => new() { Id = Id, Name = Name };
}
