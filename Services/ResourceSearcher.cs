using System.IO;
using MZResourceManager.Models;

namespace MZResourceManager.Services;

public static class ResourceSearcher
{
    private static readonly Dictionary<int, string> AudioLabels = new()
    {
        [241] = "Play BGM",
        [245] = "Play BGS",
        [249] = "Play ME",
        [250] = "Play SE",
    };

    private static readonly (string Dir, string Label)[] AudioSubFolders =
    [
        ("bgm", "BGM"), ("bgs", "BGS"), ("me", "ME"), ("se", "SE"),
    ];

    public static List<ResourceEntry> GetCategoryEntries(string gameFolder, ResourceCategory category)
    {
        var result = new List<ResourceEntry>();

        if (category == ResourceCategory.Audio)
        {
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
        }
        else
        {
            var dir = Path.Combine(gameFolder, "img", "pictures");
            if (Directory.Exists(dir))
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (name.StartsWith('.') || string.IsNullOrEmpty(name)) continue;
                    var fileDir = Path.GetDirectoryName(file)!;
                    var relative = Path.GetRelativePath(dir, fileDir);
                    var subLabel = relative == "." ? string.Empty : relative.Replace('\\', '/');
                    result.Add(new(name, subLabel));
                }
        }

        return [.. result.OrderBy(e => e.Name)];
    }

    public static List<string> GetCategoryFiles(string gameFolder, ResourceCategory category)
        => GetCategoryEntries(gameFolder, category).Select(e => e.Name).ToList();

    public static (string Path, string SubLabel)? FindFilePath(
        string gameFolder, ResourceCategory category, string baseName, string? subFolder = null)
    {
        var audioExts = new[] { ".ogg", ".m4a", ".mp3", ".wav" };
        var imageExts = new[] { ".png", ".jpg", ".jpeg", ".webp" };
        var exts = category == ResourceCategory.Audio ? audioExts : imageExts;

        if (category == ResourceCategory.Audio)
        {
            foreach (var (sub, label) in AudioSubFolders)
            {
                if (subFolder != null && !label.Equals(subFolder, StringComparison.OrdinalIgnoreCase))
                    continue;
                var dir = Path.Combine(gameFolder, "audio", sub);
                foreach (var ext in exts)
                {
                    var p = Path.Combine(dir, baseName + ext);
                    if (File.Exists(p)) return (p, label);
                }
            }
        }
        else
        {
            var dir = Path.Combine(gameFolder, "img", "pictures");
            if (!string.IsNullOrEmpty(subFolder))
            {
                var subDir = Path.Combine(dir, subFolder.Replace('/', Path.DirectorySeparatorChar));
                foreach (var ext in exts)
                {
                    var p = Path.Combine(subDir, baseName + ext);
                    if (File.Exists(p)) return (p, subFolder);
                }
            }
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                if (!string.Equals(stem, baseName, StringComparison.OrdinalIgnoreCase)) continue;
                var fileDir = Path.GetDirectoryName(file)!;
                var relative = Path.GetRelativePath(dir, fileDir);
                var label = relative == "." ? string.Empty : relative.Replace('\\', '/');
                return (file, label);
            }
        }
        return null;
    }

    public static (List<MapEventUsage> Map, List<CommonEventUsage> Common)
        Search(GameDatabase db, string? resourceName, ResourceCategory category)
    {
        var mapResults = new List<MapEventUsage>();
        var commonResults = new List<CommonEventUsage>();

        foreach (var (mapId, map) in db.Maps)
        {
            var mapName = db.MapInfos.FirstOrDefault(m => m.Id == mapId)?.Name ?? map.DisplayName;

            if (category == ResourceCategory.Audio)
            {
                if (map.AutoplayBgm && Match(map.Bgm?.Name, resourceName))
                    mapResults.Add(new(mapId, mapName, "-", "-", $"Map BGM: {map.Bgm!.Name}"));
                if (map.AutoplayBgs && Match(map.Bgs?.Name, resourceName))
                    mapResults.Add(new(mapId, mapName, "-", "-", $"Map BGS: {map.Bgs!.Name}"));
            }

            foreach (var ev in map.Events.OfType<MzEvent>())
            {
                for (int pi = 0; pi < ev.Pages.Count; pi++)
                {
                    foreach (var cmd in ev.Pages[pi].List)
                    {
                        var detail = GetDetail(cmd, resourceName, category);
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
                var detail = GetDetail(cmd, resourceName, category);
                if (detail == null) continue;
                commonResults.Add(new(ce.Id, ce.Name, detail));
            }
        }

        return (mapResults, commonResults);
    }

    private static string? GetDetail(EventCommand cmd, string? name, ResourceCategory cat) =>
        cat switch
        {
            ResourceCategory.Audio => GetAudioDetail(cmd, name),
            ResourceCategory.Pictures => GetPictureDetail(cmd, name),
            _ => null,
        };

    private static string? GetAudioDetail(EventCommand cmd, string? name)
    {
        if (!AudioLabels.TryGetValue(cmd.Code, out var label)) return null;
        var audio = cmd.GetAudioParam(0);
        if (audio == null || string.IsNullOrEmpty(audio.Name)) return null;
        if (!Match(audio.Name, name)) return null;
        return $"{label}: {audio.Name}";
    }

    private static string? GetPictureDetail(EventCommand cmd, string? name)
    {
        if (cmd.Code != 231) return null;
        var picName = cmd.GetStringParam(1);
        if (string.IsNullOrEmpty(picName) || !Match(picName, name)) return null;
        return $"Show Picture #{cmd.GetIntParam(0)}: {picName}";
    }

    private static bool Match(string? actual, string? filter) =>
        filter == null ||
        string.Equals(actual, filter, StringComparison.OrdinalIgnoreCase);
}
