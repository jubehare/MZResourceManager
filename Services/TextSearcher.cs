using MZResourceManager.Models;
using System.Text.Json;

namespace MZResourceManager.Services;

public static class TextSearcher
{
    public static (List<TextSearchResult> Map, List<TextSearchResult> Common)
        Search(GameDatabase db, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return ([], []);

        var mapResults = new List<TextSearchResult>();
        var commonResults = new List<TextSearchResult>();

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

        return (mapResults, commonResults);
    }

    private static (string Type, string? Text) Match(EventCommand cmd, string query)
    {
        var strings = ExtractStrings(cmd);
        foreach (var s in strings)
        {
            if (s.Value.Contains(query, StringComparison.OrdinalIgnoreCase))
                return (CommandLabel(cmd.Code), s.Value);
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
                        if (s.StartsWith('{') || s.StartsWith('['))
                        {
                            foreach (var inner in ExtractJsonStrings(s))
                                yield return inner;
                        }
                    }
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
                if (s.Length > 0) yield return ("json", s);
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

    private static string CommandLabel(int code) => code switch
    {
        101 => "Show Text (face)",
        401 => "Dialogue",
        102 => "Show Choices",
        402 => "Choice",
        103 => "Input Number",
        105 => "Scroll Text",
        405 => "Scroll Text body",
        111 => "Conditional Branch",
        117 => "Call Common Event",
        122 => "Control Variables",
        201 => "Transfer Player",
        231 => "Show Picture",
        241 => "Play BGM",
        245 => "Play BGS",
        249 => "Play ME",
        250 => "Play SE",
        261 => "Play Movie",
        301 => "Battle Processing",
        302 => "Shop Processing",
        320 => "Change Actor Name",
        324 => "Change Actor Nickname",
        355 => "Script",
        356 => "Plugin Command",
        _ => $"Code {code}",
    };
}
