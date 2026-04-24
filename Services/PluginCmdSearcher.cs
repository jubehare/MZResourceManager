using System.Text.Json;
using MZResourceManager.Models;

namespace MZResourceManager.Services;

public static class PluginCmdSearcher
{
    public static (
        List<PluginCmdEntry> Cmds,
        List<(PluginCmdEntry, MapEventUsage)> MapSites,
        List<(PluginCmdEntry, CommonEventUsage)> CommonSites,
        List<(PluginCmdEntry, TroopEventUsage)> TroopSites)
        CollectAll(GameDatabase db)
    {
        var mapSites = new List<(PluginCmdEntry, MapEventUsage)>();
        var commonSites = new List<(PluginCmdEntry, CommonEventUsage)>();
        var troopSites = new List<(PluginCmdEntry, TroopEventUsage)>();

        foreach (var (mapId, map) in db.Maps)
        {
            var mapName = db.MapInfos.FirstOrDefault(m => m.Id == mapId)?.Name ?? map.DisplayName;

            foreach (var ev in map.Events.OfType<MzEvent>())
            {
                for (int pi = 0; pi < ev.Pages.Count; pi++)
                {
                    foreach (var cmd in ev.Pages[pi].List)
                    {
                        if (!TryParse(cmd, out var entry, out var args)) continue;
                        mapSites.Add((entry!, new MapEventUsage(
                            mapId, mapName,
                            $"EV{ev.Id:D3} {ev.Name}",
                            $"{pi + 1}",
                            args)));
                    }
                }
            }
        }

        foreach (var ce in db.CommonEvents)
        {
            foreach (var cmd in ce.List)
            {
                if (!TryParse(cmd, out var entry, out var args)) continue;
                commonSites.Add((entry!, new CommonEventUsage(ce.Id, ce.Name, args)));
            }
        }

        foreach (var troop in db.TroopList)
        {
            for (int pi = 0; pi < troop.Pages.Count; pi++)
            {
                foreach (var cmd in troop.Pages[pi].List)
                {
                    if (!TryParse(cmd, out var entry, out var args)) continue;
                    troopSites.Add((entry!, new TroopEventUsage(troop.Id, troop.Name, pi, args)));
                }
            }
        }

        var seen = new HashSet<(string, string)>();
        var cmds = new List<PluginCmdEntry>();
        foreach (var e in mapSites.Select(s => s.Item1)
            .Concat(commonSites.Select(s => s.Item1))
            .Concat(troopSites.Select(s => s.Item1)))
        {
            if (seen.Add((e.PluginName, e.InternalCmd)))
                cmds.Add(e);
        }
        cmds = [.. cmds.OrderBy(e => e.PluginName).ThenBy(e => e.Display)];

        return (cmds, mapSites, commonSites, troopSites);
    }

    private static bool TryParse(EventCommand cmd, out PluginCmdEntry? entry, out string args)
    {
        entry = null;
        args = string.Empty;
        if (cmd.Code != MzEventCode.PluginCommand || cmd.Parameters.Length < 4) return false;

        var pluginName = Str(cmd.Parameters[0]);
        var internalCmd = Str(cmd.Parameters[1]);
        var cmdLabel = Str(cmd.Parameters[2]);

        if (string.IsNullOrEmpty(pluginName) && string.IsNullOrEmpty(internalCmd)) return false;

        entry = new PluginCmdEntry(pluginName, internalCmd, cmdLabel);
        args = BuildArgsDisplay(cmd.Parameters[3]);
        return true;
    }

    private static string Str(JsonElement el) =>
        el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : "";

    private static string BuildArgsDisplay(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object) return "";

        var parts = new List<string>();
        foreach (var prop in args.EnumerateObject())
        {
            var val = prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString() ?? ""
                : prop.Value.GetRawText();
            var key = prop.Name.Contains(':') ? prop.Name[..prop.Name.LastIndexOf(':')] : prop.Name;
            parts.Add($"{key}={val}");
        }

        var joined = string.Join("   ", parts);
        return joined.Length > 150 ? joined[..147] + "…" : joined;
    }
}
