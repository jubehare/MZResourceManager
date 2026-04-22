using CommunityToolkit.Mvvm.ComponentModel;
using MZResourceManager.Models;
using System.Collections.ObjectModel;

namespace MZResourceManager.ViewModels;

public record TilesetSheetRow(string Slot, string SheetName);

public record TilesetMapUsage(int MapId, string MapName)
{
    public string Display => $"{MapId:D3}  {MapName}";
}

public partial class TilesetAnalyzeViewModel : ObservableObject
{
    private GameDatabase? _db;
    private List<MzTileset> _all = [];

    private static readonly string[] SlotLabels =
        ["A1", "A2", "A3", "A4", "A5", "B", "C", "D", "E"];

    [ObservableProperty] private string _filterText = string.Empty;

    [ObservableProperty] private MzTileset? _selectedTileset;

    public bool ShowNoSelection => SelectedTileset == null;

    public ObservableCollection<MzTileset> FilteredTilesets { get; } = [];
    public ObservableCollection<TilesetSheetRow> Sheets { get; } = [];
    public ObservableCollection<TilesetMapUsage> MapsUsing { get; } = [];

    public void Initialize(GameDatabase db)
    {
        _db = db;
        _all = [.. db.Tilesets.Where(t => t.Id > 0 && !string.IsNullOrWhiteSpace(t.Name))];

        FilterText = string.Empty;
        SelectedTileset = null;
        RebuildFiltered();
    }


    partial void OnFilterTextChanged(string value) => RebuildFiltered();

    partial void OnSelectedTilesetChanged(MzTileset? value)
    {
        OnPropertyChanged(nameof(ShowNoSelection));
        Sheets.Clear();
        MapsUsing.Clear();

        if (value == null || _db == null) return;

        for (int i = 0; i < SlotLabels.Length; i++)
        {
            var name = i < value.TilesetNames.Length ? value.TilesetNames[i] : string.Empty;
            Sheets.Add(new TilesetSheetRow(SlotLabels[i], name ?? string.Empty));
        }

        foreach (var info in _db.MapInfos.OrderBy(m => m.Id))
        {
            if (_db.Maps.TryGetValue(info.Id, out var map) && map.TilesetId == value.Id)
                MapsUsing.Add(new TilesetMapUsage(info.Id, info.Name));
        }
    }

    private void RebuildFiltered()
    {
        var text = FilterText?.Trim() ?? string.Empty;
        FilteredTilesets.Clear();
        foreach (var t in _all)
        {
            if (!string.IsNullOrEmpty(text) &&
                !t.Name.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !t.Id.ToString().Contains(text))
                continue;
            FilteredTilesets.Add(t);
        }
    }
}
