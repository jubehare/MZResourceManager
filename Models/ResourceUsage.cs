namespace MZResourceManager.Models;

public record ResourceEntry(string Name, string SubFolder);

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

public enum ResourceCategory { Audio, Pictures }
