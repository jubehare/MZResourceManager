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

    public void Initialize(GameDatabase db)
    {
        _db = db;
        SearchQuery = string.Empty;
        StatusText = string.Empty;
        MapResults.Clear();
        CommonResults.Clear();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (_db == null || string.IsNullOrWhiteSpace(SearchQuery)) return;

        IsSearching = true;
        StatusText = $"Searching for \"{SearchQuery}\"…";
        MapResults.Clear();
        CommonResults.Clear();

        try
        {
            var db = _db;
            var query = SearchQuery.Trim();

            var (mapR, commonR) = await Task.Run(() => TextSearcher.Search(db, query));

            foreach (var r in mapR) MapResults.Add(r);
            foreach (var r in commonR) CommonResults.Add(r);

            int total = mapR.Count + commonR.Count;
            StatusText = total == 0
                ? $"No matches for \"{query}\"."
                : $"{mapR.Count} map event(s)  |  {commonR.Count} common event(s)";
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
