namespace MZResourceManager.Models;

public record ResourceEntry(string Name, string SubFolder, bool IsReserved = false);

public record MapEventUsage(
    int MapId,
    string MapName,
    string EventDisplay,
    string PageDisplay,
    string Detail)
{
    public string MapDisplay => $"{MapId:D3}  {MapName}";
}

public record CommonEventUsage(
    int EventId,
    string EventName,
    string Detail);

public record TroopEventUsage(
    int TroopId,
    string TroopName,
    int PageIndex,
    string Detail)
{
    public string TroopDisplay => $"{TroopId:D3}  {TroopName}";
    public string PageDisplay => $"{PageIndex + 1}";
}

public record TextSearchResult(
    int MapId,
    string MapName,
    string EventDisplay,
    string Page,
    string CommandType,
    string MatchedText)
{
    public string MapDisplay => $"{MapId:D3}  {MapName}";
}

public record ResourcePluginCmdUsage(
    string Context,
    string PluginName,
    string CommandName,
    string ParamKey,
    string ParamValue);

public record PluginCmdEntry(string PluginName, string InternalCmd, string CommandLabel)
{
    public string Display => string.IsNullOrWhiteSpace(CommandLabel) ? InternalCmd : CommandLabel;
}


public enum ResourceCategory { Audio, Pictures, Sprites, Animations, Backgrounds, SystemUI }
