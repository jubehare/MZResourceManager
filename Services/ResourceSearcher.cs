using System.IO;
using MZResourceManager.Models;

namespace MZResourceManager.Services;

public static class ResourceSearcher
{
    private static readonly string[] AudioExts = [".ogg", ".m4a", ".mp3", ".wav"];
    private static readonly string[] ImageExts = [".png", ".jpg", ".jpeg", ".webp"];

    private static readonly (string Sub, string Label)[] AudioSubFolders =
    [
        ("bgm", "BGM"), ("bgs", "BGS"), ("me", "ME"), ("se", "SE"),
    ];

    private static readonly Dictionary<int, string> AudioCmdLabels = new()
    {
        [MzEventCode.PlayBgm] = "Play BGM",
        [MzEventCode.PlayBgs] = "Play BGS",
        [MzEventCode.PlayMe]  = "Play ME",
        [MzEventCode.PlaySe]  = "Play SE",
    };

    // img/system/ filenames that the MZ engine loads by hardcoded convention —
    // they are ALWAYS used regardless of event content.
    private static readonly HashSet<string> ReservedSystemImages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Window",               // window skin
        "IconSet",              // master icon sheet
        "Balloon",              // balloon animations above characters
        "Shadow1",              // small character shadow
        "Shadow2",              // large character shadow
        "Damage",               // damage/recovery number sprites
        "States",               // state overlay animations
        "Weapons1",             // SV battle weapon sprite sheet 1
        "Weapons2",             // SV battle weapon sprite sheet 2
        "Weapons3",             // SV battle weapon sprite sheet 3
        "ButtonSet",            // touch-UI buttons
        "GameOver",             // game-over screen
    };

    private static readonly string SystemImgRel = Path.Combine("img", "system");

    // Maps each image category to (gameFolder-relative path, display label) pairs.
    private static readonly Dictionary<ResourceCategory, (string Rel, string Label)[]> ImageFolders = new()
    {
        [ResourceCategory.Pictures] = [(Path.Combine("img", "pictures"), "")],
        [ResourceCategory.Sprites] =
        [
            (Path.Combine("img", "characters"), "Characters"),
            (Path.Combine("img", "faces"),      "Faces"),
            (Path.Combine("img", "sv_actors"),  "SV Actors"),
            (Path.Combine("img", "sv_enemies"), "SV Enemies"),
            (Path.Combine("img", "battlers"),   "Battlers"),
        ],
        [ResourceCategory.Animations] = [(Path.Combine("img", "animations"), "")],
        [ResourceCategory.Backgrounds] =
        [
            (Path.Combine("img", "parallaxes"),   "Parallaxes"),
            (Path.Combine("img", "battlebacks1"), "Battlebacks 1"),
            (Path.Combine("img", "battlebacks2"), "Battlebacks 2"),
            (Path.Combine("img", "titles1"),      "Titles 1"),
            (Path.Combine("img", "titles2"),      "Titles 2"),
        ],
        [ResourceCategory.SystemUI] =
        [
            (Path.Combine("img", "system"), "System"),
            (Path.Combine("img", "icons"),  "Icons"),
        ],
    };

    // ── File listing ──────────────────────────────────────────────────────────

    public static List<ResourceEntry> GetCategoryEntries(string gameFolder, ResourceCategory category)
    {
        if (category == ResourceCategory.Audio)
            return GetAudioEntries(gameFolder);

        if (!ImageFolders.TryGetValue(category, out var folders))
            return [];

        var result = new List<ResourceEntry>();
        foreach (var (rel, label) in folders)
        {
            var dir = Path.Combine(gameFolder, rel);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (!ImageExts.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

                var name = Path.GetFileNameWithoutExtension(file);
                if (name.StartsWith('.') || string.IsNullOrEmpty(name)) continue;

                // Subfolder label: use folder label, plus any relative sub-path
                var fileDir  = Path.GetDirectoryName(file)!;
                var relative = Path.GetRelativePath(dir, fileDir);
                var subLabel = relative == "."
                    ? label
                    : string.IsNullOrEmpty(label)
                        ? relative.Replace('\\', '/')
                        : $"{label}/{relative.Replace('\\', '/')}";

                var isReserved = category == ResourceCategory.SystemUI
                    && string.Equals(rel, SystemImgRel, StringComparison.OrdinalIgnoreCase)
                    && ReservedSystemImages.Contains(name);

                result.Add(new(name, subLabel, isReserved));
            }
        }
        return [.. result.OrderBy(e => e.SubFolder).ThenBy(e => e.Name)];
    }

    private static List<ResourceEntry> GetAudioEntries(string gameFolder)
    {
        var result = new List<ResourceEntry>();
        foreach (var (sub, label) in AudioSubFolders)
        {
            var dir = Path.Combine(gameFolder, "audio", sub);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!name.StartsWith('.') && !string.IsNullOrEmpty(name))
                    result.Add(new(name, label));
            }
        }
        return [.. result.OrderBy(e => e.SubFolder).ThenBy(e => e.Name)];
    }

    // ── File path lookup ──────────────────────────────────────────────────────

    public static (string Path, string SubLabel)? FindFilePath(
        string gameFolder, ResourceCategory category, string baseName, string? subFolder = null)
    {
        if (category == ResourceCategory.Audio)
        {
            foreach (var (sub, label) in AudioSubFolders)
            {
                if (subFolder != null && !label.Equals(subFolder, StringComparison.OrdinalIgnoreCase))
                    continue;
                var dir = Path.Combine(gameFolder, "audio", sub);
                foreach (var ext in AudioExts)
                {
                    var p = Path.Combine(dir, baseName + ext);
                    if (File.Exists(p)) return (p, label);
                }
            }
            return null;
        }

        if (!ImageFolders.TryGetValue(category, out var folders)) return null;

        foreach (var (rel, label) in folders)
        {
            var dir = Path.Combine(gameFolder, rel);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                if (!string.Equals(stem, baseName, StringComparison.OrdinalIgnoreCase)) continue;

                var fileDir  = Path.GetDirectoryName(file)!;
                var relative = Path.GetRelativePath(dir, fileDir);
                var sublabel = relative == "."
                    ? label
                    : string.IsNullOrEmpty(label)
                        ? relative.Replace('\\', '/')
                        : $"{label}/{relative.Replace('\\', '/')}";

                // Compare against the fully-computed sublabel, not just the top-level label
                if (subFolder != null && !sublabel.Equals(subFolder, StringComparison.OrdinalIgnoreCase))
                    continue;

                return (file, sublabel);
            }
        }
        return null;
    }

    // ── Usage search ──────────────────────────────────────────────────────────

    public static (List<MapEventUsage> Map, List<CommonEventUsage> Common, List<TroopEventUsage> Troop, List<ResourcePluginCmdUsage> Plugin)
        Search(GameDatabase db, string? resourceName, ResourceCategory category)
    {
        var mapResults    = new List<MapEventUsage>();
        var commonResults = new List<CommonEventUsage>();
        var troopResults  = new List<TroopEventUsage>();
        var pluginResults = new List<ResourcePluginCmdUsage>();

        foreach (var (mapId, map) in db.Maps)
        {
            var mapName = db.MapInfos.FirstOrDefault(m => m.Id == mapId)?.Name ?? map.DisplayName;

            // Map-level audio references
            if (category == ResourceCategory.Audio)
            {
                if (map.AutoplayBgm && Match(map.Bgm?.Name, resourceName))
                    mapResults.Add(new(mapId, mapName, "-", "-", $"Map BGM: {map.Bgm!.Name}"));
                if (map.AutoplayBgs && Match(map.Bgs?.Name, resourceName))
                    mapResults.Add(new(mapId, mapName, "-", "-", $"Map BGS: {map.Bgs!.Name}"));
            }

            // Map-level image references (Backgrounds)
            if (category == ResourceCategory.Backgrounds)
            {
                if (Match(map.ParallaxName, resourceName))
                    mapResults.Add(new(mapId, mapName, "-", "-", $"Parallax: {map.ParallaxName}"));
                if (Match(map.Battleback1Name, resourceName))
                    mapResults.Add(new(mapId, mapName, "-", "-", $"Battleback 1: {map.Battleback1Name}"));
                if (Match(map.Battleback2Name, resourceName))
                    mapResults.Add(new(mapId, mapName, "-", "-", $"Battleback 2: {map.Battleback2Name}"));
            }

            foreach (var ev in map.Events.OfType<MzEvent>())
            {
                for (int pi = 0; pi < ev.Pages.Count; pi++)
                {
                    var page = ev.Pages[pi];

                    // Event page character sprite
                    if (category == ResourceCategory.Sprites)
                    {
                        var charName = page.Image?.CharacterName;
                        if (!string.IsNullOrEmpty(charName) && Match(charName, resourceName))
                            mapResults.Add(new(mapId, mapName,
                                $"EV{ev.Id:D3} {ev.Name}", $"{pi + 1}",
                                $"Character sprite: {charName}"));
                    }

                    foreach (var cmd in page.List)
                    {
                        if (TryGetPluginCmdUsage(cmd, resourceName, out var pu))
                        {
                            pluginResults.Add(pu! with { Context = $"Map {mapId:D3} {mapName} / EV{ev.Id:D3} {ev.Name} (Page {pi + 1})" });
                            continue;
                        }
                        var detail = GetCommandDetail(cmd, resourceName, category);
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
                if (TryGetPluginCmdUsage(cmd, resourceName, out var pu))
                {
                    pluginResults.Add(pu! with { Context = $"Common Event: {ce.Name}" });
                    continue;
                }
                var detail = GetCommandDetail(cmd, resourceName, category);
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
                    if (TryGetPluginCmdUsage(cmd, resourceName, out var pu))
                    {
                        pluginResults.Add(pu! with { Context = $"Troop {troop.Id:D3} {troop.Name} (Page {pi + 1})" });
                        continue;
                    }
                    var detail = GetCommandDetail(cmd, resourceName, category);
                    if (detail == null) continue;
                    troopResults.Add(new(troop.Id, troop.Name, pi, detail));
                }
            }
        }

        // Also scan plugin parameter values for resource name references
        if (resourceName != null)
        {
            foreach (var plugin in db.Plugins)
                foreach (var (key, raw) in plugin.Parameters)
                    CollectPluginParamMatches(raw, resourceName, plugin.Name, key, pluginResults);
        }

        return (mapResults, commonResults, troopResults, pluginResults);
    }

    private static string? GetCommandDetail(EventCommand cmd, string? name, ResourceCategory cat) =>
        cat switch
        {
            ResourceCategory.Audio       => GetAudioDetail(cmd, name),
            ResourceCategory.Pictures    => GetPictureDetail(cmd, name),
            ResourceCategory.Sprites     => GetFaceDetail(cmd, name),
            ResourceCategory.Backgrounds => GetBattlebackDetail(cmd, name),
            _ => null,
        };

    // Structured plugin command extractor — returns false if no match.
    // MZ code 357 layout: [0]=pluginName, [1]=internalCmd, [2]=displayLabel, [3]=args object
    private static bool TryGetPluginCmdUsage(EventCommand cmd, string? name, out ResourcePluginCmdUsage? result)
    {
        result = null;
        if (cmd.Code != MzEventCode.PluginCommand || name == null || cmd.Parameters.Length < 4) return false;

        var pluginName = cmd.Parameters[0].ValueKind == System.Text.Json.JsonValueKind.String
            ? cmd.Parameters[0].GetString() ?? "" : "";
        // Use parameters[2] = human-readable display label (e.g. "BASIC: Enter Bust")
        var cmdLabel = cmd.Parameters[2].ValueKind == System.Text.Json.JsonValueKind.String
            ? cmd.Parameters[2].GetString() ?? "" : "";

        // parameters[3] is the args object (already a JSON object, not a string)
        var args = cmd.Parameters[3];
        if (args.ValueKind != System.Text.Json.JsonValueKind.Object) return false;

        foreach (var prop in args.EnumerateObject())
        {
            if (prop.Value.ValueKind != System.Text.Json.JsonValueKind.String) continue;
            var val = prop.Value.GetString() ?? "";
            var basename = val.Contains('/') ? val[(val.LastIndexOf('/') + 1)..] : val;
            if (!string.Equals(basename, name, StringComparison.OrdinalIgnoreCase)) continue;

            result = new ResourcePluginCmdUsage("", pluginName, cmdLabel, prop.Name, val);
            return true;
        }
        return false;
    }

    // Walk a raw plugin parameter value (possibly nested JSON) for any string matching resourceName.
    private static void CollectPluginParamMatches(
        string rawValue, string resourceName,
        string pluginName, string paramKey,
        List<ResourcePluginCmdUsage> results)
    {
        WalkJsonStringForResource(rawValue.Trim(), resourceName, pluginName, paramKey, results);
    }

    private static void WalkJsonStringForResource(
        string text, string resourceName,
        string pluginName, string paramKey,
        List<ResourcePluginCmdUsage> results)
    {
        // Plain string (not JSON)
        if (!text.StartsWith('{') && !text.StartsWith('['))
        {
            var basename = text.Contains('/') ? text[(text.LastIndexOf('/') + 1)..] : text;
            if (string.Equals(basename, resourceName, StringComparison.OrdinalIgnoreCase))
                results.Add(new ResourcePluginCmdUsage("Plugin Setting", pluginName, paramKey, "", text));
            return;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(text);
            WalkJsonElement(doc.RootElement, resourceName, pluginName, paramKey, results);
        }
        catch { }
    }

    private static void WalkJsonElement(
        System.Text.Json.JsonElement el, string resourceName,
        string pluginName, string paramKey,
        List<ResourcePluginCmdUsage> results)
    {
        switch (el.ValueKind)
        {
            case System.Text.Json.JsonValueKind.String:
                var s = el.GetString() ?? "";
                // Nested JSON string (VisuMZ stores structs this way)
                if (s.StartsWith('{') || s.StartsWith('['))
                {
                    WalkJsonStringForResource(s, resourceName, pluginName, paramKey, results);
                    return;
                }
                var basename = s.Contains('/') ? s[(s.LastIndexOf('/') + 1)..] : s;
                if (string.Equals(basename, resourceName, StringComparison.OrdinalIgnoreCase))
                    results.Add(new ResourcePluginCmdUsage("Plugin Setting", pluginName, paramKey, "", s));
                break;
            case System.Text.Json.JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                    WalkJsonElement(prop.Value, resourceName, pluginName, paramKey, results);
                break;
            case System.Text.Json.JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    WalkJsonElement(item, resourceName, pluginName, paramKey, results);
                break;
        }
    }

    private static string? GetAudioDetail(EventCommand cmd, string? name)
    {
        if (!AudioCmdLabels.TryGetValue(cmd.Code, out var label)) return null;
        var audio = cmd.GetAudioParam(0);
        if (audio == null || string.IsNullOrEmpty(audio.Name)) return null;
        if (!Match(audio.Name, name)) return null;
        return $"{label}: {audio.Name}";
    }

    private static string? GetPictureDetail(EventCommand cmd, string? name)
    {
        if (cmd.Code != MzEventCode.ShowPicture) return null;
        var picName = cmd.GetStringParam(1);
        if (string.IsNullOrEmpty(picName) || !Match(picName, name)) return null;
        return $"Show Picture #{cmd.GetIntParam(0)}: {picName}";
    }

    private static string? GetFaceDetail(EventCommand cmd, string? name)
    {
        if (cmd.Code != MzEventCode.ShowText) return null;
        var faceName = cmd.GetStringParam(0);
        if (string.IsNullOrEmpty(faceName) || !Match(faceName, name)) return null;
        return $"Dialogue face: {faceName}";
    }

    private static string? GetBattlebackDetail(EventCommand cmd, string? name)
    {
        if (cmd.Code != MzEventCode.ChangeBattleback) return null;
        var bb1 = cmd.GetStringParam(0);
        var bb2 = cmd.GetStringParam(1);
        if (Match(bb1, name)) return $"Battle Back 1: {bb1}";
        if (Match(bb2, name)) return $"Battle Back 2: {bb2}";
        return null;
    }

    private static bool Match(string? actual, string? filter) =>
        filter == null ||
        string.Equals(actual, filter, StringComparison.OrdinalIgnoreCase);
}
