using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MZResourceManager.Models;
using MZResourceManager.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MZResourceManager.ViewModels;

public enum ActiveTab
{
    None,
    Maps, Items, Switches, Variables, CommonEvents,
    ResAudio, ResPictures,
    UnusedResources, TilesetAnalyze, ScriptBookExport, TextSearch
}

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    [NotifyPropertyChangedFor(nameof(ShowHome))]
    [NotifyCanExecuteChangedFor(nameof(ReloadProjectCommand))]
    private bool _projectLoaded;

    [ObservableProperty] private string _loadStatus = string.Empty;
    [ObservableProperty] private string _gameTitle = string.Empty;
    [ObservableProperty] private string _gameFolderDisplay = string.Empty;
    [ObservableProperty] private int _mapCount;
    [ObservableProperty] private int _itemCount;
    [ObservableProperty] private int _switchCount;
    [ObservableProperty] private int _variableCount;
    [ObservableProperty] private int _commonEventCount;

    [ObservableProperty] private int _audioCount;
    [ObservableProperty] private int _pictureCount;
    [ObservableProperty] private int _tilesetCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMaps))]
    [NotifyPropertyChangedFor(nameof(ShowResourceSearch))]
    [NotifyPropertyChangedFor(nameof(ShowNamedList))]
    [NotifyPropertyChangedFor(nameof(ShowUnusedResources))]
    [NotifyPropertyChangedFor(nameof(ShowTilesetAnalyze))]
    [NotifyPropertyChangedFor(nameof(ShowScriptBookExport))]
    [NotifyPropertyChangedFor(nameof(ShowTextSearch))]
    [NotifyPropertyChangedFor(nameof(ShowHome))]
    private ActiveTab _activeTab = ActiveTab.None;

    public bool ShowWelcome => !ProjectLoaded && !IsLoading;
    public bool ShowHome => ProjectLoaded && ActiveTab == ActiveTab.None;
    public bool ShowMaps => ActiveTab == ActiveTab.Maps;
    public bool ShowResourceSearch => ActiveTab is ActiveTab.ResAudio or ActiveTab.ResPictures;
    public bool ShowNamedList => ActiveTab is
        ActiveTab.Items or ActiveTab.Switches or ActiveTab.Variables or ActiveTab.CommonEvents;
    public bool ShowUnusedResources => ActiveTab == ActiveTab.UnusedResources;
    public bool ShowTilesetAnalyze => ActiveTab == ActiveTab.TilesetAnalyze;
    public bool ShowScriptBookExport => ActiveTab == ActiveTab.ScriptBookExport;
    public bool ShowTextSearch => ActiveTab == ActiveTab.TextSearch;

    [RelayCommand]
    private void NavigateHome() => ActiveTab = ActiveTab.None;

    [RelayCommand(CanExecute = nameof(ProjectLoaded))]
    private async Task ReloadProjectAsync()
    {
        if (_db?.GameFolder is { } folder)
            await LoadProjectAsync(folder);
    }

    public ResourceSearchViewModel ResourceSearch { get; } = new();
    public UnusedResourcesViewModel UnusedResources { get; } = new();
    public TilesetAnalyzeViewModel TilesetAnalyze { get; } = new();
    public ScriptBookExportViewModel ScriptBookExport { get; } = new();
    public TextSearchViewModel TextSearch { get; } = new();

    private readonly NamedListViewModel _switchList = new();
    private readonly NamedListViewModel _variableList = new();
    private readonly NamedListViewModel _commonEventList = new();
    private readonly NamedListViewModel _itemList = new();

    public ObservableCollection<MapInfoNode> MapRoots { get; } = [];
    public ObservableCollection<MapInfo> FilteredMaps { get; } = [];
    public List<MapInfo> FlatMaps { get; private set; } = [];

    [ObservableProperty] private string _mapFilterText = string.Empty;

    partial void OnMapFilterTextChanged(string value)
    {
        var text = value?.Trim() ?? string.Empty;
        FilteredMaps.Clear();
        foreach (var m in FlatMaps)
        {
            if (string.IsNullOrEmpty(text) ||
                m.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                m.Id.ToString().Contains(text))
                FilteredMaps.Add(m);
        }
    }

    [ObservableProperty] private MapInfo? _selectedMap;

    [ObservableProperty] private bool _isSearchingTransfers;
    [ObservableProperty] private string _transferStatusText = string.Empty;

    public ObservableCollection<Models.MapEventUsage> MapTransferResults { get; } = [];
    public ObservableCollection<Models.CommonEventUsage> CommonTransferResults { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExportMap))]
    private BitmapSource? _mapImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExportMap))]
    private bool _isRenderingMap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExportAll))]
    [NotifyPropertyChangedFor(nameof(ExportAllLabel))]
    private bool _isExportingAll;

    public bool CanExportMap => MapImage != null && !IsRenderingMap;
    public bool CanExportAll => _db != null && !IsExportingAll;
    public string ExportAllLabel => IsExportingAll ? "Exporting…" : "Export All Maps";

    private CancellationTokenSource? _renderCts;

    private GameDatabase? _db;
    private readonly AppSettings _settings = SettingsService.Load();

    public async Task TryAutoLoadAsync()
    {
        var folder = _settings.LastGameFolder;
        if (string.IsNullOrEmpty(folder)) return;
        if (ProjectValidator.GetValidationError(folder) != null) return;
        await LoadProjectAsync(folder);
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var dlg = new OpenFolderDialog { Title = "Select RPG Maker MZ Game Folder" };
        if (dlg.ShowDialog() != true) return;

        var error = ProjectValidator.GetValidationError(dlg.FolderName);
        if (error != null)
        {
            MessageBox.Show($"{error}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await LoadProjectAsync(dlg.FolderName);
    }

    partial void OnSelectedMapChanged(MapInfo? value)
    {
        _ = RenderSelectedMapAsync();
        _ = SearchMapTransfersAsync(value);
    }

    private async Task SearchMapTransfersAsync(MapInfo? map)
    {
        MapTransferResults.Clear();
        CommonTransferResults.Clear();

        if (map == null || _db == null)
        {
            TransferStatusText = string.Empty;
            return;
        }

        IsSearchingTransfers = true;
        TransferStatusText = $"Searching teleports to \"{map.Name}\"…";

        try
        {
            var db = _db;
            var mapId = map.Id;
            var (mapR, commonR) = await Task.Run(() =>
                Services.DatabaseEntrySearcher.SearchMapTransfers(db, mapId));

            foreach (var r in mapR) MapTransferResults.Add(r);
            foreach (var r in commonR) CommonTransferResults.Add(r);

            int total = mapR.Count + commonR.Count;
            TransferStatusText = total == 0
                ? $"No teleport events point to \"{map.Name}\"."
                : $"{mapR.Count} map event(s)  |  {commonR.Count} common event(s)";
        }
        catch (Exception ex)
        {
            TransferStatusText = $"Search error: {ex.Message}";
        }
        finally
        {
            IsSearchingTransfers = false;
        }
    }

    private async Task RenderSelectedMapAsync()
    {
        _renderCts?.Cancel();
        _renderCts = new CancellationTokenSource();
        var ct = _renderCts.Token;

        if (SelectedMap == null || _db == null) { MapImage = null; return; }

        IsRenderingMap = true;
        MapImage = null;

        try
        {
            MapImage = await MapRenderer.RenderAsync(SelectedMap.Id, _db.GameFolder, _db.Tilesets, _db.TileSize, ct);
        }
        catch (OperationCanceledException) { }
        catch { MapImage = null; }
        finally { IsRenderingMap = false; }
    }

    [RelayCommand]
    private void ExportMap()
    {
        if (MapImage == null || SelectedMap == null) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export Map as PNG",
            FileName = $"{SelectedMap.Name}.png",
            DefaultExt = ".png",
            Filter = "PNG Image|*.png"
        };
        if (dlg.ShowDialog() != true) return;

        SaveBitmapAsPng(MapImage, dlg.FileName);
    }

    [RelayCommand]
    private async Task ExportAllMapsAsync()
    {
        if (_db == null) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export All Maps as ZIP",
            FileName = $"{_db.GameTitle}_maps.zip",
            DefaultExt = ".zip",
            Filter = "ZIP Archive|*.zip"
        };
        if (dlg.ShowDialog() != true) return;

        IsExportingAll = true;
        LoadStatus = "Exporting maps…";

        try
        {
            var maps = _db.MapInfos;
            var sheets = _db.Tilesets;
            var folder = _db.GameFolder;
            var tileSize = _db.TileSize;

            await Task.Run(async () =>
            {
                using var zip = ZipFile.Open(dlg.FileName, ZipArchiveMode.Create);

                for (int i = 0; i < maps.Count; i++)
                {
                    var info = maps[i];
                    App.Current.Dispatcher.Invoke(() =>
                        LoadStatus = $"Exporting {i + 1}/{maps.Count}: {info.Name}…");

                    var bmp = await MapRenderer.RenderAsync(info.Id, folder, sheets, tileSize);
                    if (bmp == null) continue;

                    var entry = zip.CreateEntry($"{info.Name}.png", CompressionLevel.Fastest);
                    using var entryStream = entry.Open();

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bmp));
                    encoder.Save(entryStream);
                }
            });

            LoadStatus = "Export complete.";
        }
        catch (Exception ex)
        {
            LoadStatus = $"Export failed: {ex.Message}";
            MessageBox.Show($"{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsExportingAll = false;
        }
    }

    private static void SaveBitmapAsPng(BitmapSource bmp, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    [RelayCommand]
    private void Navigate(string tab)
    {
        ActiveTab = tab switch
        {
            "Maps" => ActiveTab.Maps,
            "Items" => ActiveTab.Items,
            "Switches" => ActiveTab.Switches,
            "Variables" => ActiveTab.Variables,
            "CommonEvents" => ActiveTab.CommonEvents,
            "ResAudio" => ActiveTab.ResAudio,
            "ResPictures" => ActiveTab.ResPictures,
            "UnusedResources" => ActiveTab.UnusedResources,
            "TilesetAnalyze" => ActiveTab.TilesetAnalyze,
            "ScriptBookExport" => ActiveTab.ScriptBookExport,
            "TextSearch" => ActiveTab.TextSearch,
            _ => ActiveTab.None,
        };

        if (_db != null && ShowResourceSearch)
        {
            var category = ActiveTab == ActiveTab.ResPictures
                ? ResourceCategory.Pictures
                : ResourceCategory.Audio;
            ResourceSearch.Initialize(_db, category);
        }

        if (ShowNamedList)
            OnPropertyChanged(nameof(NamedList));
    }

    public NamedListViewModel NamedList => ActiveTab switch
    {
        ActiveTab.Items => _itemList,
        ActiveTab.Switches => _switchList,
        ActiveTab.Variables => _variableList,
        ActiveTab.CommonEvents => _commonEventList,
        _ => _switchList,
    };

    private void ScanResources(string folder)
    {
        static int Count(string dir, SearchOption opt = SearchOption.TopDirectoryOnly) =>
            Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, "*", opt)
                           .Count(f => !Path.GetFileName(f).StartsWith('.'))
                : 0;

        var audio = Path.Combine(folder, "audio");
        var img = Path.Combine(folder, "img");

        AudioCount = Count(audio, SearchOption.AllDirectories);
        PictureCount = Count(Path.Combine(img, "pictures"), SearchOption.AllDirectories);
    }

    private async Task LoadProjectAsync(string folder)
    {
        IsLoading = true;
        ProjectLoaded = false;
        ActiveTab = ActiveTab.None;
        LoadStatus = "Loading…";

        try
        {
            var progress = new Progress<string>(msg => LoadStatus = msg);
            _db = await new DatabaseLoader().LoadAsync(folder, progress);

            GameTitle = _db.GameTitle;
            GameFolderDisplay = folder;
            MapCount = _db.Maps.Count;
            ItemCount = _db.Items.Count;
            SwitchCount = _db.Switches.Count;
            VariableCount = _db.Variables.Count;
            CommonEventCount = _db.CommonEvents.Count;

            TilesetCount = _db.Tilesets.Count(t => t.Id > 0 && !string.IsNullOrWhiteSpace(t.Name));
            ScanResources(folder);
            FlatMaps = [.. _db.MapInfos.OrderBy(m => m.Id)];
            OnPropertyChanged(nameof(FlatMaps));
            MapFilterText = string.Empty;
            FilteredMaps.Clear();
            foreach (var m in FlatMaps) FilteredMaps.Add(m);
            SelectedMap = FlatMaps.FirstOrDefault();
            BuildMapTree(_db.MapInfos);

            UnusedResources.Initialize(_db);
            TilesetAnalyze.Initialize(_db);
            ScriptBookExport.Initialize(_db);
            TextSearch.Initialize(_db);

            _switchList.Initialize(_db, "Switches",
                _db.Switches, Services.EntryKind.Switch);
            _variableList.Initialize(_db, "Variables",
                _db.Variables, Services.EntryKind.Variable);
            _itemList.Initialize(_db, "Items",
                _db.Items, Services.EntryKind.Item);
            _commonEventList.Initialize(_db, "Common Events",
                _db.CommonEvents.Select(ce => new Models.NamedEntry { Id = ce.Id, Name = ce.Name }),
                Services.EntryKind.CommonEvent);

            _settings.LastGameFolder = folder;
            SettingsService.Save(_settings);
            ProjectLoaded = true;
            OnPropertyChanged(nameof(CanExportAll));
        }
        catch (Exception ex)
        {
            LoadStatus = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to load project:\n\n{ex.Message}", "Load Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildMapTree(List<MapInfo> mapInfos)
    {
        MapRoots.Clear();
        var lookup = mapInfos.ToDictionary(m => m.Id, m => new MapInfoNode { Info = m });

        foreach (var node in lookup.Values)
        {
            if (node.Info.ParentId == 0 || !lookup.ContainsKey(node.Info.ParentId))
                MapRoots.Add(node);
            else
                lookup[node.Info.ParentId].Children.Add(node);
        }
    }
}
