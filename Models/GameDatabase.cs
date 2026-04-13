namespace MZResourceManager.Models;

public class GameDatabase
{
    public string GameFolder { get; set; } = string.Empty;
    public string GameTitle { get; set; } = string.Empty;
    public int TileSize { get; set; } = 48;

    public List<NamedEntry> Items { get; set; } = [];
    public List<MzItem> ItemDetails { get; set; } = [];
    public List<NamedEntry> Weapons { get; set; } = [];
    public List<NamedEntry> Armors { get; set; } = [];
    public List<NamedEntry> Actors { get; set; } = [];
    public List<NamedEntry> Classes { get; set; } = [];
    public List<NamedEntry> Skills { get; set; } = [];
    public List<NamedEntry> States { get; set; } = [];
    public List<NamedEntry> Enemies { get; set; } = [];
    public List<MzTroop> TroopList { get; set; } = [];
    public List<NamedEntry> Switches { get; set; } = [];
    public List<NamedEntry> Variables { get; set; } = [];
    public List<MapInfo> MapInfos { get; set; } = [];
    public Dictionary<int, MzMap> Maps { get; set; } = [];
    public List<MzTileset> Tilesets { get; set; } = [];
    public List<MzCommonEvent> CommonEvents { get; set; } = [];
    public List<MzPlugin> Plugins { get; set; } = [];
}
