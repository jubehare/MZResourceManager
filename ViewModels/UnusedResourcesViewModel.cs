using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MZResourceManager.Models;
using MZResourceManager.Services;
using System.Collections.ObjectModel;

namespace MZResourceManager.ViewModels;

public partial class UnusedResourcesViewModel : ObservableObject
{
    private GameDatabase? _db;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private bool _isScanning;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _hasScanned;

    [ObservableProperty] private string _audioFilterText = string.Empty;
    [ObservableProperty] private string _pictureFilterText = string.Empty;

    private List<UnusedFile> _allAudio = [];
    private List<UnusedFile> _allPictures = [];

    public ObservableCollection<UnusedFile> FilteredAudio { get; } = [];
    public ObservableCollection<UnusedFile> FilteredPictures { get; } = [];

    public void Initialize(GameDatabase db)
    {
        _db = db;
        HasScanned = false;
        StatusText = "Click Scan to detect unused audio and image files.";
        _allAudio = [];
        _allPictures = [];
        FilteredAudio.Clear();
        FilteredPictures.Clear();
        AudioFilterText = string.Empty;
        PictureFilterText = string.Empty;
        ScanCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (_db == null) return;

        IsScanning = true;
        HasScanned = false;
        StatusText = "Scanning…";
        FilteredAudio.Clear();
        FilteredPictures.Clear();

        try
        {
            var db = _db;
            var (audio, pictures) = await Task.Run(() => UnusedFileDetector.Detect(db));

            _allAudio = audio;
            _allPictures = pictures;
            HasScanned = true;
            RebuildAudio();
            RebuildPictures();

            StatusText = audio.Count + pictures.Count == 0
                ? "No unused files found."
                : $"{audio.Count} unused audio  |  {pictures.Count} unused image(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private bool CanScan() => _db != null && !IsScanning;

    partial void OnAudioFilterTextChanged(string value) => RebuildAudio();
    partial void OnPictureFilterTextChanged(string value) => RebuildPictures();

    private void RebuildAudio()
    {
        var text = AudioFilterText?.Trim() ?? string.Empty;
        FilteredAudio.Clear();
        foreach (var f in _allAudio)
        {
            if (!string.IsNullOrEmpty(text) &&
                !f.Name.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !f.SubFolder.Contains(text, StringComparison.OrdinalIgnoreCase))
                continue;
            FilteredAudio.Add(f);
        }
    }

    private void RebuildPictures()
    {
        var text = PictureFilterText?.Trim() ?? string.Empty;
        FilteredPictures.Clear();
        foreach (var f in _allPictures)
        {
            if (!string.IsNullOrEmpty(text) &&
                !f.Name.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !f.SubFolder.Contains(text, StringComparison.OrdinalIgnoreCase))
                continue;
            FilteredPictures.Add(f);
        }
    }
}
