using MZResourceManager.Models;
using System.Text.Json;

namespace MZResourceManager.Services;

public static class TextSearcher
{
    public static (
        List<TextSearchResult> Map,
        List<TextSearchResult> Common,
        List<TextSearchResult> Troop,
        List<PluginParamUsage> Plugin)
        Search(GameDatabase db, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return ([], [], [], []);

        var mapResults = new List<TextSearchResult>();
        var commonResults = new List<TextSearchResult>();
        var troopResults = new List<TextSearchResult>();

        foreach (var (mapId, map) in db.Maps)
        {
            var mapName = db.MapInfos.FirstOrDefault(m => m.Id == mapId)?.Name ?? map.DisplayName;

            foreach (var ev in map.Events.OfType<MzEvent>())
            {
                for (int pi = 0; pi < ev.Pages.Count; pi++)
                {
                    foreach (var cmd in ev.Pages[pi].List)
                    {
                        var (type, text) = Match(cmd, query);
                        if (text == null) continue;
                        mapResults.Add(new(mapId, mapName,
                            $"EV{ev.Id:D3} {ev.Name}", $"{pi + 1}", type, text));
                    }
                }
            }
        }

        foreach (var ce in db.CommonEvents)
        {
            foreach (var cmd in ce.List)
            {
                var (type, text) = Match(cmd, query);
                if (text == null) continue;
                commonResults.Add(new(0, ce.Name, ce.Name, "", type, text));
            }
        }

        foreach (var troop in db.TroopList)
        {
            for (int pi = 0; pi < troop.Pages.Count; pi++)
            {
                foreach (var cmd in troop.Pages[pi].List)
                {
                    var (type, text) = Match(cmd, query);
                    if (text == null) continue;
                    troopResults.Add(new(troop.Id, troop.Name,
                        $"Page {pi + 1}", $"{pi + 1}", type, text));
                }
            }
        }

        var pluginResults = SearchPluginParams(db, query);

        return (mapResults, commonResults, troopResults, pluginResults);
    }

    // Scans every enabled plugin's parameter values for strings matching query.
    // Handles plain strings, single-encoded JSON, and VisuMZ-style double-encoded structs.
    private static List<PluginParamUsage> SearchPluginParams(GameDatabase db, string query)
    {
        var results = new List<PluginParamUsage>();
        foreach (var plugin in db.Plugins)
            foreach (var (key, rawValue) in plugin.Parameters)
                WalkParamString(rawValue, query, plugin.Name, key, results);
        return results;
    }

    private static void WalkParamString(
        string raw, string query,
        string pluginName, string path,
        List<PluginParamUsage> results)
    {
        var v = raw.Trim();
        if (v.StartsWith('{') || v.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(v);
                WalkParamJson(doc.RootElement, query, pluginName, path, results);
            }
            catch { }
        }
        else if (v.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            results.Add(new(pluginName, path, v));
        }
    }

    private static void WalkParamJson(
        JsonElement el, string query,
        string pluginName, string path,
        List<PluginParamUsage> results)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                var s = el.GetString() ?? "";
                // VisuMZ stores nested structs as double-encoded JSON strings
                if (s.StartsWith('{') || s.StartsWith('['))
                    WalkParamString(s, query, pluginName, path, results);
                else if (s.Contains(query, StringComparison.OrdinalIgnoreCase))
                    results.Add(new(pluginName, path, s));
                break;
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                    WalkParamJson(prop.Value, query, pluginName, $"{path} › {prop.Name}", results);
                break;
            case JsonValueKind.Array:
                int i = 0;
                foreach (var item in el.EnumerateArray())
                    WalkParamJson(item, query, pluginName, $"{path}[{i++}]", results);
                break;
        }
    }

    private static (string Type, string? Text) Match(EventCommand cmd, string query)
    {
        foreach (var (_, value) in ExtractStrings(cmd))
        {
            if (value.Contains(query, StringComparison.OrdinalIgnoreCase))
                return (MzEventCode.Label(cmd.Code), value);
        }
        return (string.Empty, null);
    }

    private static IEnumerable<(string Key, string Value)> ExtractStrings(EventCommand cmd)
    {
        for (int i = 0; i < cmd.Parameters.Length; i++)
        {
            var p = cmd.Parameters[i];
            switch (p.ValueKind)
            {
                case JsonValueKind.String:
                    var s = p.GetString() ?? string.Empty;
                    if (s.Length > 0)
                    {
                        yield return ($"p{i}", s);
                        // Walk nested JSON strings (e.g. VisuMZ struct parameters)
                        if (s.StartsWith('{') || s.StartsWith('['))
                            foreach (var inner in ExtractJsonStrings(s))
                                yield return inner;
                    }
                    break;

                // Walk JSON objects/arrays directly — covers code-357 args object
                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    foreach (var r in WalkJson(p))
                        yield return r;
                    break;
            }
        }
    }

    private static IEnumerable<(string Key, string Value)> ExtractJsonStrings(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch { yield break; }

        using (doc)
            foreach (var item in WalkJson(doc.RootElement))
                yield return item;
    }

    private static IEnumerable<(string, string)> WalkJson(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                var s = el.GetString() ?? string.Empty;
                if (s.Length > 0)
                {
                    yield return ("json", s);
                    if (s.StartsWith('{') || s.StartsWith('['))
                        foreach (var r in ExtractJsonStrings(s))
                            yield return r;
                }
                break;
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                    foreach (var r in WalkJson(p.Value))
                        yield return r;
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    foreach (var r in WalkJson(item))
                        yield return r;
                break;
        }
    }

}
