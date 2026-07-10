using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using MZResourceManager.Models;

namespace MZResourceManager.Services;

public class DatabaseLoader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public async Task<GameDatabase> LoadAsync(string gameFolder, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var db = new GameDatabase { GameFolder = gameFolder };
        var dataDir = Path.Combine(gameFolder, "data");

        progress?.Report("Loading system data…");
        var system = await LoadJsonAsync<MzSystem>(Path.Combine(dataDir, "System.json"), ct);
        db.GameTitle = system.GameTitle;
        db.TileSize = system.Advanced.TileSize > 0 ? system.Advanced.TileSize : 48;
        db.Switches = BuildNamedList(system.Switches);
        db.Variables = BuildNamedList(system.Variables);
        db.System = system;

        progress?.Report("Loading plugins…");
        db.Plugins = await LoadPluginsAsync(gameFolder, ct);
        LoadPluginParamTypes(db.Plugins, gameFolder);

        progress?.Report("Loading database…");
        var itemsTask = LoadNullableListAsync<MzItem>(Path.Combine(dataDir, "Items.json"), ct);
        var weaponsTask = LoadNamedListAsync(Path.Combine(dataDir, "Weapons.json"), ct);
        var armorsTask = LoadNamedListAsync(Path.Combine(dataDir, "Armors.json"), ct);
        var actorsTask = LoadNamedListAsync(Path.Combine(dataDir, "Actors.json"), ct);
        var actorDetailsTask = LoadNullableListAsync<MzActor>(Path.Combine(dataDir, "Actors.json"), ct);
        var classesTask = LoadNamedListAsync(Path.Combine(dataDir, "Classes.json"), ct);
        var skillsTask = LoadNamedListAsync(Path.Combine(dataDir, "Skills.json"), ct);
        var statesTask = LoadNamedListAsync(Path.Combine(dataDir, "States.json"), ct);
        var enemiesTask = LoadNamedListAsync(Path.Combine(dataDir, "Enemies.json"), ct);
        await Task.WhenAll(itemsTask, weaponsTask, armorsTask, actorsTask, actorDetailsTask, classesTask, skillsTask, statesTask, enemiesTask);
        db.ItemDetails = await itemsTask;
        db.Items = db.ItemDetails.Select(i => i.ToNamedEntry()).ToList();
        db.Weapons = await weaponsTask;
        db.Armors = await armorsTask;
        db.Actors = await actorsTask;
        db.ActorDetails = await actorDetailsTask;
        db.Classes = await classesTask;
        db.Skills = await skillsTask;
        db.States = await statesTask;
        db.Enemies = await enemiesTask;

        progress?.Report("Loading tilesets, events, troops…");
        db.Tilesets = await LoadNullableListAsync<MzTileset>(Path.Combine(dataDir, "Tilesets.json"), ct);
        db.CommonEvents = await LoadNullableListAsync<MzCommonEvent>(Path.Combine(dataDir, "CommonEvents.json"), ct);
        db.TroopList = await LoadNullableListAsync<MzTroop>(Path.Combine(dataDir, "Troops.json"), ct);
        db.MapInfos = await LoadNullableListAsync<MapInfo>(Path.Combine(dataDir, "MapInfos.json"), ct);

        progress?.Report("Loading maps…");
        db.Maps = await LoadAllMapsAsync(dataDir, ct);

        progress?.Report($"Loaded: {db.Maps.Count} maps · {db.Items.Count} items · {db.CommonEvents.Count} common events");
        return db;
    }

    private async Task<T> LoadJsonAsync<T>(string path, CancellationToken ct) where T : new()
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOpts, ct) ?? new T();
    }

    private async Task<List<NamedEntry>> LoadNamedListAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var arr = await JsonSerializer.DeserializeAsync<NamedEntry?[]>(stream, JsonOpts, ct) ?? [];
        return [.. arr.Where(x => x != null).Select(x => x!)];
    }

    private async Task<List<T>> LoadNullableListAsync<T>(string path, CancellationToken ct) where T : class
    {
        await using var stream = File.OpenRead(path);
        var arr = await JsonSerializer.DeserializeAsync<T?[]>(stream, JsonOpts, ct) ?? [];
        return [.. arr.Where(x => x != null).Select(x => x!)];
    }

    private async Task<Dictionary<int, MzMap>> LoadAllMapsAsync(string dataDir, CancellationToken ct)
    {
        var mapFiles = Directory.EnumerateFiles(dataDir, "Map???.json")
            .Where(f => Regex.IsMatch(Path.GetFileNameWithoutExtension(f), @"^Map\d{3}$"));

        var tasks = mapFiles.Select(async file =>
        {
            var id = int.Parse(Path.GetFileNameWithoutExtension(file)[3..]);
            var map = await LoadJsonAsync<MzMap>(file, ct);
            return (id, map);
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(r => r.id, r => r.map);
    }

    private async Task<List<MzPlugin>> LoadPluginsAsync(string gameFolder, CancellationToken ct)
    {
        var path = Path.Combine(gameFolder, "js", "plugins.js");
        if (!File.Exists(path)) return [];
        try
        {
            var content = await File.ReadAllTextAsync(path, System.Text.Encoding.UTF8, ct);
            var start = content.IndexOf('[');
            if (start < 0) return [];
            var json = content[start..].TrimEnd().TrimEnd(';');
            var all = JsonSerializer.Deserialize<List<MzPlugin>>(json, JsonOpts) ?? [];
            return all.Where(p => p.Status).ToList();
        }
        catch { return []; }
    }

    private static void LoadPluginParamTypes(List<MzPlugin> plugins, string gameFolder)
    {
        var pluginsDir = Path.Combine(gameFolder, "js", "plugins");
        if (!Directory.Exists(pluginsDir)) return;

        foreach (var plugin in plugins)
        {
            var jsPath = Path.Combine(pluginsDir, plugin.Name + ".js");
            if (File.Exists(jsPath))
                plugin.ParamTypes = PluginParamParser.ParseParamTypes(jsPath);
        }
    }

    private static List<NamedEntry> BuildNamedList(string[] names)
    {
        var list = new List<NamedEntry>(names.Length);
        for (int i = 1; i < names.Length; i++)
            list.Add(new NamedEntry { Id = i, Name = names[i] ?? string.Empty });
        return list;
    }
}
