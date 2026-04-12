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

public enum ActiveTab { None, Maps, Items, Switches, Variables, CommonEvents }

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private bool _projectLoaded;

    [ObservableProperty] private string _loadStatus = string.Empty;
    [ObservableProperty] private string _gameTitle = string.Empty;
    [ObservableProperty] private string _gameFolderDisplay = string.Empty;
    [ObservableProperty] private int _mapCount;
    [ObservableProperty] private int _itemCount;
    [ObservableProperty] private int _switchCount;
    [ObservableProperty] private int _variableCount;
    [ObservableProperty] private int _commonEventCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMaps))]
    private ActiveTab _activeTab = ActiveTab.None;

    public bool ShowWelcome => !ProjectLoaded && !IsLoading;
    public bool ShowMaps => ActiveTab == ActiveTab.Maps;

    public ObservableCollection<MapInfoNode> MapRoots { get; } = [];
    public List<MapInfo> FlatMaps { get; private set; } = [];

    [ObservableProperty] private MapInfo? _selectedMap;

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
            MapImage = await MapRenderer.RenderAsync(SelectedMap.Id, _db.GameFolder, _db.Tilesets, ct);
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

            await Task.Run(async () =>
            {
                using var zip = ZipFile.Open(dlg.FileName, ZipArchiveMode.Create);

                for (int i = 0; i < maps.Count; i++)
                {
                    var info = maps[i];
                    App.Current.Dispatcher.Invoke(() =>
                        LoadStatus = $"Exporting {i + 1}/{maps.Count}: {info.Name}…");

                    var bmp = await MapRenderer.RenderAsync(info.Id, folder, sheets);
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
            _ => ActiveTab.None,
        };
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

            FlatMaps = [.. _db.MapInfos.OrderBy(m => m.Id)];
            OnPropertyChanged(nameof(FlatMaps));
            SelectedMap = FlatMaps.FirstOrDefault();
            BuildMapTree(_db.MapInfos);

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
