using CommunityToolkit.Mvvm.ComponentModel;
using MZResourceManager.Models;
using MZResourceManager.Services;
using System.Collections.ObjectModel;

namespace MZResourceManager.ViewModels;

public partial class NamedListViewModel : ObservableObject
{
    private GameDatabase? _db;
    private EntryKind _kind;
    private Dictionary<int, MzItem> _itemLookup = [];

    [ObservableProperty] private string _title = string.Empty;

    public ObservableCollection<NamedEntry> AllEntries { get; } = [];
    [ObservableProperty] private string _filterText = string.Empty;

    [ObservableProperty] private NamedEntry? _selectedEntry;

    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string _statusText = string.Empty;

    public bool ShowNoSelection => SelectedEntry == null;
    public bool ShowItemDetail => _kind == EntryKind.Item && SelectedEntry != null;

    [ObservableProperty] private MzItem? _selectedItemDetail;

    public ObservableCollection<MapEventUsage> MapResults { get; } = [];
    public ObservableCollection<CommonEventUsage> CommonResults { get; } = [];
    public ObservableCollection<TroopEventUsage> TroopResults { get; } = [];
    public ObservableCollection<PluginParamUsage> PluginResults { get; } = [];

    public ObservableCollection<NamedEntry> FilteredEntries { get; } = [];

    public void Initialize(GameDatabase db, string title, IEnumerable<NamedEntry> entries, EntryKind kind)
    {
        _db = db;
        _kind = kind;
        Title = title;

        _itemLookup = kind == EntryKind.Item
            ? db.ItemDetails.ToDictionary(i => i.Id)
            : [];

        AllEntries.Clear();
        FilteredEntries.Clear();
        foreach (var e in entries.Where(e => e.Id > 0 && !string.IsNullOrWhiteSpace(e.Name)))
        {
            AllEntries.Add(e);
            FilteredEntries.Add(e);
        }

        FilterText = string.Empty;
        SelectedEntry = null;
        SelectedItemDetail = null;
        MapResults.Clear();
        CommonResults.Clear();
        TroopResults.Clear();
        PluginResults.Clear();
        StatusText = $"{AllEntries.Count} entries — pick one to find usages.";
    }

    // ── Reactive ──────────────────────────────────────────────────────────────

    partial void OnFilterTextChanged(string value) => RebuildFiltered();

    partial void OnSelectedEntryChanged(NamedEntry? value)
    {
        OnPropertyChanged(nameof(ShowNoSelection));
        OnPropertyChanged(nameof(ShowItemDetail));
        MapResults.Clear();
        CommonResults.Clear();
        TroopResults.Clear();
        PluginResults.Clear();

        if (value == null)
        {
            SelectedItemDetail = null;
            StatusText = $"{AllEntries.Count} entries — pick one to find usages.";
            return;
        }

        SelectedItemDetail = _itemLookup.TryGetValue(value.Id, out var item) ? item : null;
        _ = SearchAsync(value);
    }

    // ── List filtering (combo dropdown) ──────────────────────────────────────

    private void RebuildFiltered()
    {
        var text = FilterText?.Trim() ?? string.Empty;
        FilteredEntries.Clear();
        foreach (var e in AllEntries)
        {
            if (!string.IsNullOrEmpty(text) &&
                !e.Name.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !e.Id.ToString().Contains(text))
                continue;
            FilteredEntries.Add(e);
        }
    }

    private async System.Threading.Tasks.Task SearchAsync(NamedEntry entry)
    {
        if (_db == null) return;

        IsSearching = true;
        StatusText = $"Searching for \"{entry.Name}\"…";

        try
        {
            var db = _db;
            var kind = _kind;
            var id = entry.Id;

            var (mapR, commonR, troopR, pluginR) = await System.Threading.Tasks.Task.Run(() =>
            {
                var (m, c, t) = DatabaseEntrySearcher.Search(db, id, kind);
                var p = DatabaseEntrySearcher.SearchPluginParams(db, id, kind);
                return (m, c, t, p);
            });

            MapResults.Clear();
            foreach (var r in mapR) MapResults.Add(r);
            CommonResults.Clear();
            foreach (var r in commonR) CommonResults.Add(r);
            TroopResults.Clear();
            foreach (var r in troopR) TroopResults.Add(r);
            PluginResults.Clear();
            foreach (var r in pluginR) PluginResults.Add(r);

            int total = mapR.Count + commonR.Count + troopR.Count + pluginR.Count;
            StatusText = total == 0
                ? $"No usages found for \"{entry.Name}\"."
                : $"{mapR.Count} map  |  {commonR.Count} common  |  {troopR.Count} battle  |  {pluginR.Count} plugin param(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Search error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }
}
