using MZResourceManager.Models;

namespace MZResourceManager.Services;

public enum EntryKind { Switch, Variable, Item, CommonEvent }

public static class DatabaseEntrySearcher
{
    public static (List<MapEventUsage> Map, List<CommonEventUsage> Common, List<TroopEventUsage> Troop)
        Search(GameDatabase db, int entryId, EntryKind kind)
    {
        var mapResults = new List<MapEventUsage>();
        var commonResults = new List<CommonEventUsage>();
        var troopResults = new List<TroopEventUsage>();

        foreach (var (mapId, map) in db.Maps)
        {
            var mapName = db.MapInfos.FirstOrDefault(m => m.Id == mapId)?.Name ?? map.DisplayName;

            foreach (var ev in map.Events.OfType<MzEvent>())
            {
                for (int pi = 0; pi < ev.Pages.Count; pi++)
                {
                    foreach (var cmd in ev.Pages[pi].List)
                    {
                        var detail = GetDetail(cmd, entryId, kind);
                        if (detail == null) continue;
                        mapResults.Add(new(mapId, mapName,
                            $"EV{ev.Id:D3} {ev.Name}", $"{pi + 1}", detail));
                    }
                }
            }
        }

        foreach (var ce in db.CommonEvents)
        {
            foreach (var cmd in ce.List)
            {
                var detail = GetDetail(cmd, entryId, kind);
                if (detail == null) continue;
                commonResults.Add(new(ce.Id, ce.Name, detail));
            }
        }

        foreach (var troop in db.TroopList)
        {
            for (int pi = 0; pi < troop.Pages.Count; pi++)
            {
                foreach (var cmd in troop.Pages[pi].List)
                {
                    var detail = GetDetail(cmd, entryId, kind);
                    if (detail == null) continue;
                    troopResults.Add(new(troop.Id, troop.Name, pi, detail));
                }
            }
        }

        return (mapResults, commonResults, troopResults);
    }

    public static List<PluginParamUsage> SearchPluginParams(GameDatabase db, int id, EntryKind kind)
    {
        var results = new List<PluginParamUsage>();
        var idStr = id.ToString();

        string[] matchTypes = kind switch
        {
            EntryKind.Variable => ["variable"],
            EntryKind.Switch => ["switch"],
            EntryKind.CommonEvent => ["common_event"],
            EntryKind.Item => ["item"],
            _ => [],
        };
        if (matchTypes.Length == 0) return results;

        foreach (var plugin in db.Plugins)
        {
            foreach (var (key, rawValue) in plugin.Parameters)
            {

                if (!plugin.ParamTypes.TryGetValue(key, out var type)) continue;

                var baseType = type.TrimEnd('[', ']');
                if (!matchTypes.Contains(baseType, StringComparer.OrdinalIgnoreCase)) continue;

                if (ContainsId(rawValue, idStr))
                    results.Add(new(plugin.Name, key, rawValue));
            }
        }

        return results;
    }

    private static bool ContainsId(string rawValue, string idStr)
    {
        var v = rawValue.Trim();

        // WIP
        if (v == idStr) return true;

        if (v.StartsWith('['))
        {
            foreach (var token in v.Split([',', '[', ']', ' '], StringSplitOptions.RemoveEmptyEntries))
                if (token.Trim() == idStr) return true;
            return false;
        }

        if (v.StartsWith('{'))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(v);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var inner = prop.Value.ValueKind == System.Text.Json.JsonValueKind.String
                        ? prop.Value.GetString() ?? ""
                        : prop.Value.GetRawText();
                    if (ContainsId(inner, idStr)) return true;
                }
            }
            catch
            {
                //skip invalid JSON
            }
            return false;
        }

        return false;
    }


    // Finds all events that transfer the player directly to mapId
    public static (List<MapEventUsage> Map, List<CommonEventUsage> Common, List<TroopEventUsage> Troop)
        SearchMapTransfers(GameDatabase db, int mapId)
    {
        var mapResults = new List<MapEventUsage>();
        var commonResults = new List<CommonEventUsage>();
        var troopResults = new List<TroopEventUsage>();

        foreach (var (srcMapId, map) in db.Maps)
        {
            var mapName = db.MapInfos.FirstOrDefault(m => m.Id == srcMapId)?.Name ?? map.DisplayName;

            foreach (var ev in map.Events.OfType<MzEvent>())
            {
                for (int pi = 0; pi < ev.Pages.Count; pi++)
                {
                    foreach (var cmd in ev.Pages[pi].List)
                    {
                        var detail = GetTransferDetail(cmd, mapId);
                        if (detail == null) continue;
                        mapResults.Add(new(srcMapId, mapName,
                            $"EV{ev.Id:D3} {ev.Name}", $"{pi + 1}", detail));
                    }
                }
            }
        }

        foreach (var ce in db.CommonEvents)
        {
            foreach (var cmd in ce.List)
            {
                var detail = GetTransferDetail(cmd, mapId);
                if (detail == null) continue;
                commonResults.Add(new(ce.Id, ce.Name, detail));
            }
        }

        foreach (var troop in db.TroopList.Where(t => t.Id > 0))
        {
            for (int pi = 0; pi < troop.Pages.Count; pi++)
            {
                foreach (var cmd in troop.Pages[pi].List)
                {
                    var detail = GetTransferDetail(cmd, mapId);
                    if (detail == null) continue;
                    troopResults.Add(new(troop.Id, troop.Name, pi, detail));
                }
            }
        }

        return (mapResults, commonResults, troopResults);
    }

    private static string? GetTransferDetail(EventCommand cmd, int mapId)
    {
        if (cmd.Code != MzEventCode.TransferPlayer) return null;
        // params[0]: 0 = direct map, 1 = variable
        if (cmd.GetIntParam(0) == 0 && cmd.GetIntParam(1) == mapId)
        {
            int x = cmd.GetIntParam(2), y = cmd.GetIntParam(3);
            return $"Transfer Player → ({x}, {y})";
        }
        return null;
    }

    private static string? GetDetail(EventCommand cmd, int id, EntryKind kind) =>
        kind switch
        {
            EntryKind.Switch => GetSwitchDetail(cmd, id),
            EntryKind.Variable => GetVariableDetail(cmd, id),
            EntryKind.Item => GetItemDetail(cmd, id),
            EntryKind.CommonEvent => GetCommonEventDetail(cmd, id),
            _ => null,
        };

    private static string? GetSwitchDetail(EventCommand cmd, int id)
    {
        switch (cmd.Code)
        {
            case MzEventCode.ConditionalBranch when cmd.GetIntParam(0) == 0 && cmd.GetIntParam(1) == id:
                {
                    var state = cmd.GetIntParam(2) == 1 ? "ON" : "OFF";
                    return $"Condition: Switch [{id:D4}] is {state}";
                }

            case MzEventCode.ControlSwitches when cmd.GetIntParam(0) <= id && id <= cmd.GetIntParam(1):
                {
                    var start = cmd.GetIntParam(0);
                    var end = cmd.GetIntParam(1);
                    var state = cmd.GetIntParam(2) == 0 ? "ON" : cmd.GetIntParam(2) == 1 ? "OFF" : "Toggle";
                    var range = start == end ? $"[{start:D4}]" : $"[{start:D4}–{end:D4}]";
                    return $"Control Switches {range} → {state}";
                }

            default: return null;
        }
    }

    private static string? GetVariableDetail(EventCommand cmd, int id)
    {
        switch (cmd.Code)
        {
            case MzEventCode.ConditionalBranch when cmd.GetIntParam(0) == 1 && cmd.GetIntParam(1) == id:
                return $"Condition: Variable [{id:D4}]";

            case MzEventCode.ControlVariables when cmd.GetIntParam(0) <= id && id <= cmd.GetIntParam(1):
                {
                    var start = cmd.GetIntParam(0);
                    var end = cmd.GetIntParam(1);
                    var range = start == end ? $"[{start:D4}]" : $"[{start:D4}–{end:D4}]";
                    return $"Control Variables {range}";
                }

            default: return null;
        }
    }

    private static string? GetItemDetail(EventCommand cmd, int id)
    {
        switch (cmd.Code)
        {
            case MzEventCode.ChangeItems when cmd.GetIntParam(0) == id:
                {
                    var op = cmd.GetIntParam(1) == 0 ? "+" : "−";
                    var amt = cmd.GetIntParam(3);
                    return $"Change Items [{id:D4}] {op}{amt}";
                }

            case MzEventCode.ConditionalBranch when cmd.GetIntParam(0) == 9 && cmd.GetIntParam(1) == id:
                return $"Condition: Has Item [{id:D4}]";

            default: return null;
        }
    }

    private static string? GetCommonEventDetail(EventCommand cmd, int id)
    {
        if (cmd.Code == MzEventCode.CallCommonEvent && cmd.GetIntParam(0) == id)
            return $"Call Common Event [{id:D3}]";
        return null;
    }
}
