using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MZResourceManager.Models;
using MZResourceManager.Services;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace MZResourceManager.ViewModels;

public partial class UnusedResourcesViewModel : ObservableObject
{
    private GameDatabase? _db;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    [NotifyPropertyChangedFor(nameof(ShowScanning))]
    [NotifyPropertyChangedFor(nameof(ShowResults))]
    private bool _isScanning;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    [NotifyPropertyChangedFor(nameof(ShowResults))]
    private bool _hasScanned;

    public bool ShowWelcome => !HasScanned && !IsScanning;
    public bool ShowScanning => IsScanning;
    public bool ShowResults => HasScanned && !IsScanning;

    [ObservableProperty] private string _audioSelectedSubFolder = "All";
    [ObservableProperty] private UnusedFile? _selectedAudioFile;
    [ObservableProperty] private string? _audioPreviewFilePath;
    [ObservableProperty] private string _audioTypeLabel = string.Empty;

    [ObservableProperty] private string _pictureSelectedSubFolder = "All";
    [ObservableProperty] private UnusedFile? _selectedPictureFile;
    [ObservableProperty] private BitmapSource? _picturePreviewImageSource;

    private List<UnusedFile> _allAudio = [];
    private List<UnusedFile> _allPictures = [];

    public ObservableCollection<SubFolderOption> AudioSubFolderOptions { get; } = [];
    public ObservableCollection<SubFolderOption> PictureSubFolderOptions { get; } = [];
    public ObservableCollection<UnusedFile> FilteredAudio { get; } = [];
    public ObservableCollection<UnusedFile> FilteredPictures { get; } = [];

    public bool ShowAudioSelection => SelectedAudioFile != null;
    public bool ShowPictureSelection => SelectedPictureFile != null;

    public void Initialize(GameDatabase db)
    {
        _db = db;
        HasScanned = false;
        StatusText = "Click Scan to detect unused audio and image files.";
        _allAudio = [];
        _allPictures = [];
        FilteredAudio.Clear();
        FilteredPictures.Clear();
        AudioSubFolderOptions.Clear();
        PictureSubFolderOptions.Clear();
        AudioSelectedSubFolder = "All";
        PictureSelectedSubFolder = "All";
        SelectedAudioFile = null;
        SelectedPictureFile = null;
        AudioPreviewFilePath = null;
        PicturePreviewImageSource = null;
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
        SelectedAudioFile = null;
        SelectedPictureFile = null;
        AudioPreviewFilePath = null;
        PicturePreviewImageSource = null;

        try
        {
            var db = _db;
            var (audio, pictures) = await Task.Run(() => UnusedFileDetector.Detect(db));

            _allAudio = audio;
            _allPictures = pictures;
            HasScanned = true;

            AudioSubFolderOptions.Clear();
            AudioSubFolderOptions.Add(new SubFolderOption("All") { IsSelected = true });
            foreach (var label in audio.Select(f => f.SubFolder)
                                       .Where(s => !string.IsNullOrEmpty(s))
                                       .Distinct())
                AudioSubFolderOptions.Add(new SubFolderOption(label));

            PictureSubFolderOptions.Clear();
            PictureSubFolderOptions.Add(new SubFolderOption("All") { IsSelected = true });
            foreach (var label in pictures.Select(f => f.SubFolder)
                                          .Where(s => !string.IsNullOrEmpty(s))
                                          .Distinct())
                PictureSubFolderOptions.Add(new SubFolderOption(label));

            AudioSelectedSubFolder = "All";
            PictureSelectedSubFolder = "All";
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

    [RelayCommand]
    private void SelectAudioSubFolder(SubFolderOption option) => AudioSelectedSubFolder = option.Name;

    [RelayCommand]
    private void SelectPictureSubFolder(SubFolderOption option) => PictureSelectedSubFolder = option.Name;

    partial void OnAudioSelectedSubFolderChanged(string value)
    {
        foreach (var opt in AudioSubFolderOptions)
            opt.IsSelected = opt.Name == value;
        RebuildAudio();
    }

    partial void OnPictureSelectedSubFolderChanged(string value)
    {
        foreach (var opt in PictureSubFolderOptions)
            opt.IsSelected = opt.Name == value;
        RebuildPictures();
    }

    partial void OnSelectedAudioFileChanged(UnusedFile? value)
    {
        OnPropertyChanged(nameof(ShowAudioSelection));
        AudioPreviewFilePath = value?.FullPath;
        AudioTypeLabel = value?.SubFolder ?? string.Empty;
    }

    partial void OnSelectedPictureFileChanged(UnusedFile? value)
    {
        OnPropertyChanged(nameof(ShowPictureSelection));
        PicturePreviewImageSource = null;
        if (value == null) return;

        var path = value.FullPath;
        _ = Task.Run(() =>
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
                App.Current.Dispatcher.Invoke(() => PicturePreviewImageSource = bmp);
            }
            catch { }
        });
    }

    [RelayCommand]
    private void OpenAudioFile()
    {
        if (SelectedAudioFile?.FullPath is { } path)
            TryOpen(path);
    }

    [RelayCommand]
    private void OpenPictureFile()
    {
        if (SelectedPictureFile?.FullPath is { } path)
            TryOpen(path);
    }

    private static void TryOpen(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }
    }

    private void RebuildAudio()
    {
        var folder = AudioSelectedSubFolder;
        FilteredAudio.Clear();
        foreach (var f in _allAudio)
        {
            if (folder != "All" && f.SubFolder != folder) continue;
            FilteredAudio.Add(f);
        }
    }

    private void RebuildPictures()
    {
        var folder = PictureSelectedSubFolder;
        FilteredPictures.Clear();
        foreach (var f in _allPictures)
        {
            if (folder != "All" && f.SubFolder != folder) continue;
            FilteredPictures.Add(f);
        }
    }
}
