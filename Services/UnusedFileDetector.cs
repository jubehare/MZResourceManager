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

    private static readonly (string Sub, string Label)[] AudioSubDirs =
    [
        ("bgm", "BGM"), ("bgs", "BGS"), ("me", "ME"), ("se", "SE"),
    ];

    public static (List<UnusedFile> Audio, List<UnusedFile> Pictures) Detect(GameDatabase db)
    {
        var usedAudio = CollectUsedAudio(db);
        var usedPictures = CollectUsedPictures(db);

        var unusedAudio = ScanAudioFiles(db.GameFolder, usedAudio);
        var unusedPictures = ScanPictureFiles(db.GameFolder, usedPictures);

        return (unusedAudio, unusedPictures);
    }

    private static HashSet<string> CollectUsedAudio(GameDatabase db)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

    private static HashSet<string> CollectUsedPictures(GameDatabase db)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var map in db.Maps.Values)
        {
            AddPicture(used, map.ParallaxName);
            AddPicture(used, map.Battleback1Name);
            AddPicture(used, map.Battleback2Name);
        }

        // Event commands (Show Picture = code 231)
        foreach (var cmd in AllCommands(db))
        {
            if (cmd.Code == 231)
                AddPicture(used, cmd.GetStringParam(1));
        }

        return used;
    }

    private static List<UnusedFile> ScanAudioFiles(string gameFolder, HashSet<string> used)
    {
        var result = new List<UnusedFile>();

        foreach (var (sub, label) in AudioSubDirs)
        {
            var dir = Path.Combine(gameFolder, "audio", sub);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (!AudioExts.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    continue;
                var stem = Path.GetFileNameWithoutExtension(file);
                if (stem.StartsWith('.') || string.IsNullOrEmpty(stem)) continue;
                if (!used.Contains(stem))
                    result.Add(new(stem, label, file));
            }
        }

        return [.. result.OrderBy(f => f.SubFolder).ThenBy(f => f.Name)];
    }

    private static List<UnusedFile> ScanPictureFiles(string gameFolder, HashSet<string> used)
    {
        var result = new List<UnusedFile>();
        var baseDir = Path.Combine(gameFolder, "img", "pictures");
        if (!Directory.Exists(baseDir)) return result;

        foreach (var file in Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories))
        {
            if (!ImageExts.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                continue;
            var stem = Path.GetFileNameWithoutExtension(file);
            if (stem.StartsWith('.') || string.IsNullOrEmpty(stem)) continue;

            var fileDir = Path.GetDirectoryName(file)!;
            var relative = Path.GetRelativePath(baseDir, fileDir);
            var subLabel = relative == "." ? string.Empty : relative.Replace('\\', '/');

            var qualifiedKey = string.IsNullOrEmpty(subLabel) ? stem : $"{subLabel}/{stem}";
            if (!used.Contains(stem) && !used.Contains(qualifiedKey))
                result.Add(new(stem, subLabel, file));
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

    private static void AddPicture(HashSet<string> set, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name)) set.Add(name);
    }
}
