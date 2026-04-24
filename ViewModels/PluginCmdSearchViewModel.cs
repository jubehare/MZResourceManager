using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using MZResourceManager.Models;
using MZResourceManager.Services;

namespace MZResourceManager.ViewModels;

public class PluginCmdListItem
{
    public PluginCmdEntry Entry { get; init; } = null!;
    public int CallCount { get; init; }

    public string Display => Entry.Display;
    public string Sub => $"{Entry.PluginName}  ·  {CallCount} call{(CallCount == 1 ? "" : "s")}";
}

public partial class PluginCmdSearchViewModel : ObservableObject
{
    private List<(PluginCmdEntry Entry, MapEventUsage Site)> _mapSites = [];
    private List<(PluginCmdEntry Entry, CommonEventUsage Site)> _commonSites = [];
    private List<(PluginCmdEntry Entry, TroopEventUsage Site)> _troopSites = [];
    private List<PluginCmdListItem> _allItems = [];

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private PluginCmdListItem? _selectedItem;

    public ObservableCollection<PluginCmdListItem> FilteredItems { get; } = [];
    public ObservableCollection<MapEventUsage> MapCallSites { get; } = [];
    public ObservableCollection<CommonEventUsage> CommonCallSites { get; } = [];
    public ObservableCollection<TroopEventUsage> TroopCallSites { get; } = [];

    public void Initialize(GameDatabase db)
    {
        FilterText = string.Empty;
        SelectedItem = null;
        ClearCallSites();
        FilteredItems.Clear();
        _allItems = [];
        _mapSites = [];
        _commonSites = [];
        _troopSites = [];
        StatusText = "Scanning plugin commands…";
        IsLoading = true;

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            var (cmds, mapSites, commonSites, troopSites) = PluginCmdSearcher.CollectAll(db);

            var counts = new Dictionary<(string, string), int>();
            void Count(PluginCmdEntry e)
            {
                var k = (e.PluginName, e.InternalCmd);
                counts[k] = counts.TryGetValue(k, out var c) ? c + 1 : 1;
            }
            foreach (var (e, _) in mapSites) Count(e);
            foreach (var (e, _) in commonSites) Count(e);
            foreach (var (e, _) in troopSites) Count(e);

            var items = cmds.Select(e => new PluginCmdListItem
            {
                Entry = e,
                CallCount = counts.TryGetValue((e.PluginName, e.InternalCmd), out var n) ? n : 0,
            }).ToList();

            App.Current.Dispatcher.Invoke(() =>
            {
                _allItems = items;
                _mapSites = mapSites;
                _commonSites = commonSites;
                _troopSites = troopSites;
                IsLoading = false;
                StatusText = $"{items.Count} unique command type(s) — select one to see call sites.";
                RebuildList();
            });
        });
    }

    partial void OnFilterTextChanged(string value) => RebuildList();

    partial void OnSelectedItemChanged(PluginCmdListItem? value)
    {
        ClearCallSites();
        if (value == null)
        {
            StatusText = $"{_allItems.Count} unique command type(s) — select one to see call sites.";
            return;
        }

        bool Match(PluginCmdEntry e) =>
            e.PluginName == value.Entry.PluginName &&
            e.InternalCmd == value.Entry.InternalCmd;

        foreach (var (e, s) in _mapSites) if (Match(e)) MapCallSites.Add(s);
        foreach (var (e, s) in _commonSites) if (Match(e)) CommonCallSites.Add(s);
        foreach (var (e, s) in _troopSites) if (Match(e)) TroopCallSites.Add(s);

        int map = MapCallSites.Count, common = CommonCallSites.Count, troop = TroopCallSites.Count;
        int total = map + common + troop;
        StatusText = total == 0
            ? $"No call sites found for \"{value.Display}\"."
            : $"{map} map  |  {common} common  |  {troop} battle  —  \"{value.Display}\"";
    }

    private void ClearCallSites()
    {
        MapCallSites.Clear();
        CommonCallSites.Clear();
        TroopCallSites.Clear();
    }

    private void RebuildList()
    {
        var text = FilterText?.Trim() ?? string.Empty;
        FilteredItems.Clear();
        foreach (var item in _allItems)
        {
            if (!string.IsNullOrEmpty(text) &&
                !item.Entry.Display.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !item.Entry.PluginName.Contains(text, StringComparison.OrdinalIgnoreCase))
                continue;
            FilteredItems.Add(item);
        }
    }
}
