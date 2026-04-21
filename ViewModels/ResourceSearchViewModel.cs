using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using MZResourceManager.Models;
using MZResourceManager.Services;

namespace MZResourceManager.ViewModels;

public partial class ResourceSearchViewModel : ObservableObject
{
    private GameDatabase? _db;
    private ResourceCategory _category;
    private List<ResourceEntry> _allEntries = [];

    [ObservableProperty] private string _categoryTitle = string.Empty;
    [ObservableProperty] private bool _isAudioCategory;
    [ObservableProperty] private bool _isImageCategory;

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string _selectedSubFolder = "All";

    [ObservableProperty] private ResourceEntry? _selectedEntry;
    [ObservableProperty] private BitmapSource? _previewImageSource;
    [ObservableProperty] private string? _audioFilePath;
    [ObservableProperty] private string _audioTypeLabel = string.Empty;

    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string _statusText = string.Empty;

    public bool ShowNoSelection => SelectedEntry == null;
    public bool ShowAudioPreview => IsAudioCategory && SelectedEntry != null;
    public bool ShowImagePreview => IsImageCategory && SelectedEntry != null;

    public ObservableCollection<string> SubFolderOptions { get; } = [];
    public ObservableCollection<ResourceEntry> FilteredEntries { get; } = [];
    public ObservableCollection<MapEventUsage> MapResults { get; } = [];
    public ObservableCollection<CommonEventUsage> CommonResults { get; } = [];

    public void Initialize(GameDatabase db, ResourceCategory category)
    {
        _db = db;
        _category = category;

        IsAudioCategory = category == ResourceCategory.Audio;
        IsImageCategory = category == ResourceCategory.Pictures;

        CategoryTitle = category == ResourceCategory.Audio ? "Audio" : "Pictures";

        _allEntries = ResourceSearcher.GetCategoryEntries(db.GameFolder, category);

        SubFolderOptions.Clear();
        SubFolderOptions.Add("All");
        foreach (var label in _allEntries.Select(e => e.SubFolder).Where(s => !string.IsNullOrEmpty(s)).Distinct())
            SubFolderOptions.Add(label);

        SelectedSubFolder = "All";
        FilterText = string.Empty;
        RebuildList();

        SelectedEntry = null;
        PreviewImageSource = null;
        AudioFilePath = null;
        AudioTypeLabel = string.Empty;
        MapResults.Clear();
        CommonResults.Clear();
        StatusText = $"{_allEntries.Count} files — select one to search.";
    }

    partial void OnFilterTextChanged(string value) => RebuildList();
    partial void OnSelectedSubFolderChanged(string value) => RebuildList();

    partial void OnSelectedEntryChanged(ResourceEntry? value)
    {
        OnPropertyChanged(nameof(ShowNoSelection));
        OnPropertyChanged(nameof(ShowAudioPreview));
        OnPropertyChanged(nameof(ShowImagePreview));

        PreviewImageSource = null;
        AudioFilePath = null;
        AudioTypeLabel = string.Empty;

        if (value == null)
        {
            MapResults.Clear();
            CommonResults.Clear();
            StatusText = $"{_allEntries.Count} files — select one to search.";
            return;
        }

        LoadPreview(value);
        _ = SearchAsync(value.Name);
    }

    [RelayCommand]
    private void SelectSubFolder(string folder) => SelectedSubFolder = folder;

    private void LoadPreview(ResourceEntry entry)
    {
        if (_db == null) return;

        var found = ResourceSearcher.FindFilePath(
            _db.GameFolder, _category, entry.Name,
            string.IsNullOrEmpty(entry.SubFolder) ? null : entry.SubFolder);

        if (found == null) return;

        if (_category == ResourceCategory.Audio)
        {
            AudioTypeLabel = found.Value.SubLabel;
            AudioFilePath = found.Value.Path;
        }
        else
        {
            var path = found.Value.Path;
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 400;
                    bmp.EndInit();
                    bmp.Freeze();
                    App.Current.Dispatcher.Invoke(() => PreviewImageSource = bmp);
                }
                catch { }
            });
        }
    }

    private async System.Threading.Tasks.Task SearchAsync(string resourceName)
    {
        if (_db == null) return;

        IsSearching = true;
        StatusText = $"Searching for \"{resourceName}\"...";

        try
        {
            var db = _db;
            var category = _category;
            var (mapR, commonR) = await System.Threading.Tasks.Task.Run(() =>
                ResourceSearcher.Search(db, resourceName, category));

            MapResults.Clear();
            foreach (var r in mapR) MapResults.Add(r);
            CommonResults.Clear();
            foreach (var r in commonR) CommonResults.Add(r);

            int total = mapR.Count + commonR.Count;
            StatusText = total == 0
                ? $"No usages found for \"{resourceName}\"."
                : $"{mapR.Count} map event(s)  |  {commonR.Count} common event(s)";
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

    private void RebuildList()
    {
        var text = FilterText?.Trim() ?? string.Empty;
        var folder = SelectedSubFolder;

        FilteredEntries.Clear();
        foreach (var e in _allEntries)
        {
            if (folder != "All" && e.SubFolder != folder) continue;
            if (!string.IsNullOrEmpty(text) &&
                !e.Name.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
            FilteredEntries.Add(e);
        }
    }
}
