using System.IO;
using System.Text.Json;
using MZResourceManager.Models;

namespace MZResourceManager.Services;

public record UnusedFile(string Name, string SubFolder, string FullPath)
{
    public string Display => string.IsNullOrEmpty(SubFolder) ? Name : $"{SubFolder}/{Name}";
}

public static class UnusedFileDetector
{
    private static readonly string[] AudioExts = [".ogg", ".m4a", ".mp3", ".wav"];
    private static readonly string[] ImageExts = [".png", ".jpg", ".jpeg", ".webp"];

    private static readonly (string Dir, string Label)[] AudioDirs =
    [
        (Path.Combine("audio", "bgm"), "BGM"),
        (Path.Combine("audio", "bgs"), "BGS"),
        (Path.Combine("audio", "me"),  "ME"),
        (Path.Combine("audio", "se"),  "SE"),
    ];

    private static readonly (string Dir, string Label)[] ImageDirs =
    [
        (Path.Combine("img", "pictures"),     "pictures"),
        (Path.Combine("img", "parallaxes"),   "parallaxes"),
        (Path.Combine("img", "battlebacks1"), "battlebacks1"),
        (Path.Combine("img", "battlebacks2"), "battlebacks2"),
        (Path.Combine("img", "titles1"),      "titles1"),
        (Path.Combine("img", "titles2"),      "titles2"),
    ];

    public static (List<UnusedFile> Audio, List<UnusedFile> Pictures) Detect(GameDatabase db)
    {
        var usedAudio = CollectUsedAudio(db);
        var usedImages = CollectUsedImages(db);

        // Collect every string value that appears in plugin command args or plugin
        // parameter settings — a file whose stem appears here is considered used
        // regardless of the specific command that references it.
        var pluginRefs = CollectPluginResourceRefs(db);

        var unusedAudio = ScanFiles(db.GameFolder, AudioDirs, AudioExts, usedAudio, pluginRefs);
        var unusedImages = ScanFiles(db.GameFolder, ImageDirs, ImageExts, usedImages, pluginRefs);

        return (unusedAudio, unusedImages);
    }

    // ── Used-resource collectors ──────────────────────────────────────────────

    private static HashSet<string> CollectUsedAudio(GameDatabase db)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddName(used, db.System.TitleBgm?.Name);
        AddName(used, db.System.BattleBgm?.Name);
        AddName(used, db.System.DefeatMe?.Name);
        AddName(used, db.System.VictoryMe?.Name);

        foreach (var map in db.Maps.Values)
        {
            AddName(used, map.Bgm?.Name);
            AddName(used, map.Bgs?.Name);
        }

        foreach (var cmd in AllCommands(db))
        {
            if (cmd.Code is MzEventCode.PlayBgm or MzEventCode.PlayBgs or MzEventCode.PlayMe or MzEventCode.PlaySe)
                AddName(used, cmd.GetAudioParam(0)?.Name);
        }

        return used;
    }

    private static HashSet<string> CollectUsedImages(GameDatabase db)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // System.json references
        AddName(used, db.System.Title1Name);
        AddName(used, db.System.Title2Name);
        AddName(used, db.System.Battleback1Name);
        AddName(used, db.System.Battleback2Name);

        // Map-level references
        foreach (var map in db.Maps.Values)
        {
            AddName(used, map.ParallaxName);
            AddName(used, map.Battleback1Name);
            AddName(used, map.Battleback2Name);
        }

        // Event commands
        foreach (var cmd in AllCommands(db))
        {
            switch (cmd.Code)
            {
                case MzEventCode.ShowPicture: AddName(used, cmd.GetStringParam(1)); break;
                case MzEventCode.ChangeBattleback:                                    // Change Battle Back
                    AddName(used, cmd.GetStringParam(0));
                    AddName(used, cmd.GetStringParam(1));
                    break;
            }
        }

        return used;
    }

    // ── Plugin resource reference collector ───────────────────────────────────

    /// <summary>
    /// Walks every plugin command arg object (code 357 params[3]) and every
    /// plugin.Parameters value from plugins.js, collecting all leaf string stems.
    /// A "stem" is the last path segment: "Bust/MySprite_A" → "MySprite_A".
    /// </summary>
    private static HashSet<string> CollectPluginResourceRefs(GameDatabase db)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Plugin command args embedded in events
        foreach (var cmd in AllCommands(db))
        {
            if (cmd.Code != MzEventCode.PluginCommand || cmd.Parameters.Length < 4) continue;
            if (cmd.Parameters[3].ValueKind == JsonValueKind.Object)
                WalkJsonElement(cmd.Parameters[3], refs);
        }

        // Static plugin parameter values from plugins.js
        foreach (var plugin in db.Plugins)
            foreach (var (_, rawValue) in plugin.Parameters)
                WalkString(rawValue, refs);

        return refs;
    }

    private static void WalkString(string raw, HashSet<string> refs)
    {
        var v = raw.Trim();
        if (v.StartsWith('{') || v.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(v);
                WalkJsonElement(doc.RootElement, refs);
            }
            catch { }
        }
        else
        {
            AddStem(refs, v);
        }
    }

    private static void WalkJsonElement(JsonElement el, HashSet<string> refs)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                var s = el.GetString() ?? "";
                // VisuMZ double-encoded struct strings
                if (s.StartsWith('{') || s.StartsWith('['))
                    WalkString(s, refs);
                else
                    AddStem(refs, s);
                break;
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                    WalkJsonElement(prop.Value, refs);
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    WalkJsonElement(item, refs);
                break;
        }
    }

    /// <summary>Adds the filename stem (last path segment, no extension) to the set.</summary>
    private static void AddStem(HashSet<string> refs, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        // Strip path prefix: "Bust/MySprite_A_0" → "MySprite_A_0"
        var stem = value.Contains('/') || value.Contains('\\')
            ? Path.GetFileNameWithoutExtension(value.Replace('\\', '/').Split('/')[^1])
            : Path.GetFileNameWithoutExtension(value);
        if (!string.IsNullOrWhiteSpace(stem))
            refs.Add(stem);
    }

    // ── File scanning ─────────────────────────────────────────────────────────

    private static List<UnusedFile> ScanFiles(
        string gameFolder,
        (string Dir, string Label)[] dirs,
        string[] extensions,
        HashSet<string> used,
        HashSet<string> pluginRefs)
    {
        var result = new List<UnusedFile>();

        foreach (var (rel, label) in dirs)
        {
            var dir = Path.Combine(gameFolder, rel);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (!extensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

                var stem = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(stem) || stem.StartsWith('.')) continue;

                // File is used if referenced by an explicit event command OR
                // by any string value found in plugin commands / plugin parameters.
                if (used.Contains(stem) || pluginRefs.Contains(stem)) continue;

                var fileDir = Path.GetDirectoryName(file)!;
                var relative = Path.GetRelativePath(dir, fileDir);
                var subLabel = relative == "." ? label : $"{label}/{relative.Replace('\\', '/')}";

                result.Add(new UnusedFile(stem, subLabel, file));
            }
        }

        return [.. result.OrderBy(f => f.SubFolder).ThenBy(f => f.Name)];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>All event commands across maps (including troops) and common events.</summary>
    private static IEnumerable<EventCommand> AllCommands(GameDatabase db)
    {
        foreach (var map in db.Maps.Values)
            foreach (var ev in map.Events.OfType<MzEvent>())
                foreach (var page in ev.Pages)
                    foreach (var cmd in page.List)
                        yield return cmd;

        foreach (var ce in db.CommonEvents)
            foreach (var cmd in ce.List)
                yield return cmd;

        // Troop (battle) events — previously missing
        foreach (var troop in db.TroopList)
            foreach (var page in troop.Pages)
                foreach (var cmd in page.List)
                    yield return cmd;
    }

    private static void AddName(HashSet<string> set, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name)) set.Add(name);
    }
}
