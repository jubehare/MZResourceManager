using System.IO;
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
        (Path.Combine("img", "pictures"),    "pictures"),
        (Path.Combine("img", "parallaxes"),  "parallaxes"),
        (Path.Combine("img", "battlebacks1"),"battlebacks1"),
        (Path.Combine("img", "battlebacks2"),"battlebacks2"),
        (Path.Combine("img", "titles1"),     "titles1"),
        (Path.Combine("img", "titles2"),     "titles2"),
    ];

    public static (List<UnusedFile> Audio, List<UnusedFile> Pictures) Detect(GameDatabase db)
    {
        var usedAudio = CollectUsedAudio(db);
        var usedImages = CollectUsedImages(db);

        var unusedAudio = ScanFiles(db.GameFolder, AudioDirs, AudioExts, usedAudio);
        var unusedImages = ScanFiles(db.GameFolder, ImageDirs, ImageExts, usedImages);

        return (unusedAudio, unusedImages);
    }

    private static HashSet<string> CollectUsedAudio(GameDatabase db)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddAudio(used, db.System.TitleBgm?.Name);
        AddAudio(used, db.System.BattleBgm?.Name);
        AddAudio(used, db.System.DefeatMe?.Name);
        AddAudio(used, db.System.VictoryMe?.Name);

        foreach (var map in db.Maps.Values)
        {
            AddAudio(used, map.Bgm?.Name);
            AddAudio(used, map.Bgs?.Name);
        }

        foreach (var cmd in AllCommands(db))
        {
            if (cmd.Code is 241 or 245 or 249 or 250)
                AddAudio(used, cmd.GetAudioParam(0)?.Name);
        }

        return used;
    }


    private static HashSet<string> CollectUsedImages(GameDatabase db)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // System images
        AddImage(used, db.System.Title1Name);
        AddImage(used, db.System.Title2Name);
        AddImage(used, db.System.Battleback1Name);
        AddImage(used, db.System.Battleback2Name);

        // Map images
        foreach (var map in db.Maps.Values)
        {
            AddImage(used, map.ParallaxName);
            AddImage(used, map.Battleback1Name);
            AddImage(used, map.Battleback2Name);
        }

        // Event commands: Show Picture (231)
        foreach (var cmd in AllCommands(db))
        {
            if (cmd.Code == 231)
                AddImage(used, cmd.GetStringParam(1));
        }

        return used;
    }


    private static List<UnusedFile> ScanFiles(
        string gameFolder,
        (string Dir, string Label)[] dirs,
        string[] extensions,
        HashSet<string> used)
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

                var fileDir = Path.GetDirectoryName(file)!;
                var relative = Path.GetRelativePath(dir, fileDir);
                var subLabel = relative == "." ? label : $"{label}/{relative.Replace('\\', '/')}";

                if (!used.Contains(stem))
                    result.Add(new UnusedFile(stem, subLabel, file));
            }
        }

        return [.. result.OrderBy(f => f.SubFolder).ThenBy(f => f.Name)];
    }


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
    }

    private static void AddAudio(HashSet<string> set, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name)) set.Add(name);
    }

    private static void AddImage(HashSet<string> set, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name)) set.Add(name);
    }
}
