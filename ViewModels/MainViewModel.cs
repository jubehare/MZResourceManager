using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MZResourceManager.Models;
using MZResourceManager.Services;
using System.Windows;

namespace MZResourceManager.ViewModels;

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

    public bool ShowWelcome => !ProjectLoaded && !IsLoading;

    private GameDatabase? _db;

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

    private async Task LoadProjectAsync(string folder)
    {
        IsLoading = true;
        ProjectLoaded = false;
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

            ProjectLoaded = true;
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
}
