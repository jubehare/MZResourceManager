using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using MZResourceManager.Models;
using MZResourceManager.Services;

namespace MZResourceManager.ViewModels;

public partial class SubFolderOption : ObservableObject
{
    public string Name { get; }
    [ObservableProperty] private bool _isSelected;
    public SubFolderOption(string name) => Name = name;
}

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

    [ObservableProperty] private string? _imageFilePath;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string _statusText = string.Empty;

    public bool ShowNoSelection => SelectedEntry == null;
    public bool ShowAudioPreview => IsAudioCategory && SelectedEntry != null;
    public bool ShowImagePreview => IsImageCategory && SelectedEntry != null;

    public ObservableCollection<SubFolderOption> SubFolderOptions { get; } = [];
    public ObservableCollection<ResourceEntry> FilteredEntries { get; } = [];
    public ObservableCollection<MapEventUsage> MapResults { get; } = [];
    public ObservableCollection<CommonEventUsage> CommonResults { get; } = [];
    public ObservableCollection<TroopEventUsage> TroopResults { get; } = [];
    public ObservableCollection<ResourcePluginCmdUsage> PluginCmdResults { get; } = [];

    public void Initialize(GameDatabase db, ResourceCategory category)
    {
        _db = db;
        _category = category;

        IsAudioCategory = category == ResourceCategory.Audio;
        IsImageCategory = category != ResourceCategory.Audio;

        CategoryTitle = category switch
        {
            ResourceCategory.Audio       => "Audio",
            ResourceCategory.Sprites     => "Sprites",
            ResourceCategory.Animations  => "Animations",
            ResourceCategory.Backgrounds => "Backgrounds",
            ResourceCategory.SystemUI    => "System / UI",
            _                            => "Pictures",
        };

        _allEntries = ResourceSearcher.GetCategoryEntries(db.GameFolder, category);

        SubFolderOptions.Clear();
        SubFolderOptions.Add(new SubFolderOption("All") { IsSelected = true });
        foreach (var label in _allEntries.Select(e => e.SubFolder).Where(s => !string.IsNullOrEmpty(s)).Distinct())
            SubFolderOptions.Add(new SubFolderOption(label));

        SelectedSubFolder = "All";
        FilterText = string.Empty;
        RebuildList();

        SelectedEntry = null;
        PreviewImageSource = null;
        AudioFilePath = null;
        ImageFilePath = null;
        AudioTypeLabel = string.Empty;
        MapResults.Clear();
        CommonResults.Clear();
        TroopResults.Clear();
        PluginCmdResults.Clear();
        StatusText = $"{_allEntries.Count} files — select one to search.";
    }

    partial void OnFilterTextChanged(string value) => RebuildList();

    partial void OnSelectedSubFolderChanged(string value)
    {
        foreach (var opt in SubFolderOptions)
            opt.IsSelected = opt.Name == value;
        RebuildList();
    }

    partial void OnSelectedEntryChanged(ResourceEntry? value)
    {
        OnPropertyChanged(nameof(ShowNoSelection));
        OnPropertyChanged(nameof(ShowAudioPreview));
        OnPropertyChanged(nameof(ShowImagePreview));

        PreviewImageSource = null;
        AudioFilePath = null;
        ImageFilePath = null;
        AudioTypeLabel = string.Empty;

        if (value == null)
        {
            MapResults.Clear();
            CommonResults.Clear();
            TroopResults.Clear();
        PluginCmdResults.Clear();
            StatusText = $"{_allEntries.Count} files — select one to search.";
            return;
        }

        LoadPreview(value);
        _ = SearchAsync(value.Name);
    }

    [RelayCommand]
    private void SelectSubFolder(SubFolderOption option) => SelectedSubFolder = option.Name;

    [RelayCommand]
    private void OpenFile()
    {
        var path = ImageFilePath ?? AudioFilePath;
        if (path == null) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { }
    }

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
            AudioFilePath  = found.Value.Path;
        }
        else
        {
            var path = found.Value.Path;
            ImageFilePath = path;
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
            var (mapR, commonR, troopR, pluginR) = await System.Threading.Tasks.Task.Run(() =>
                ResourceSearcher.Search(db, resourceName, category));

            MapResults.Clear();
            foreach (var r in mapR) MapResults.Add(r);
            CommonResults.Clear();
            foreach (var r in commonR) CommonResults.Add(r);
            TroopResults.Clear();
            PluginCmdResults.Clear();
            foreach (var r in troopR) TroopResults.Add(r);
            foreach (var r in pluginR) PluginCmdResults.Add(r);

            int total = mapR.Count + commonR.Count + troopR.Count + pluginR.Count;
            StatusText = total == 0
                ? $"No usages found for \"{resourceName}\"."
                : $"{mapR.Count} map  |  {commonR.Count} common  |  {troopR.Count} battle  |  {pluginR.Count} plugin cmd(s)";
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
