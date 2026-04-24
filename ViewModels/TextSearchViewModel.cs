using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MZResourceManager.Models;
using MZResourceManager.Services;
using System.Collections.ObjectModel;

namespace MZResourceManager.ViewModels;

public partial class TextSearchViewModel : ObservableObject
{
    private GameDatabase? _db;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string _statusText = string.Empty;

    public ObservableCollection<TextSearchResult> MapResults { get; } = [];
    public ObservableCollection<TextSearchResult> CommonResults { get; } = [];
    public ObservableCollection<TextSearchResult> TroopResults { get; } = [];
    public ObservableCollection<PluginParamUsage> PluginResults { get; } = [];

    public void Initialize(GameDatabase db)
    {
        _db = db;
        SearchQuery = string.Empty;
        StatusText = string.Empty;
        MapResults.Clear();
        CommonResults.Clear();
        TroopResults.Clear();
        PluginResults.Clear();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (_db == null || string.IsNullOrWhiteSpace(SearchQuery)) return;

        IsSearching = true;
        StatusText = $"Searching for \"{SearchQuery}\"…";
        MapResults.Clear();
        CommonResults.Clear();
        TroopResults.Clear();
        PluginResults.Clear();

        try
        {
            var db = _db;
            var query = SearchQuery.Trim();

            var (mapR, commonR, troopR, pluginR) =
                await Task.Run(() => TextSearcher.Search(db, query));

            foreach (var r in mapR) MapResults.Add(r);
            foreach (var r in commonR) CommonResults.Add(r);
            foreach (var r in troopR) TroopResults.Add(r);
            foreach (var r in pluginR) PluginResults.Add(r);

            int total = mapR.Count + commonR.Count + troopR.Count + pluginR.Count;
            StatusText = total == 0
                ? $"No matches for \"{query}\"."
                : $"{mapR.Count} map  |  {commonR.Count} common  |  {troopR.Count} battle  |  {pluginR.Count} plugin setting(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    public void TriggerSearch() => _ = SearchAsync();
}
