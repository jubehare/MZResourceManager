using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MZResourceManager.ViewModels;
using NAudio.Vorbis;
using NAudio.Wave;

namespace MZResourceManager.Views;

public partial class ResourceSearchView : UserControl, IDisposable
{
    private ResourceSearchViewModel? _vm;
    private WaveOutEvent? _waveOut;
    private WaveStream? _reader;

    public ResourceSearchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as ResourceSearchViewModel;

        if (_vm != null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResourceSearchViewModel.AudioFilePath))
            StopAndDispose();
    }

    private void PlayFile(string path)
    {
        try
        {
            _reader = Path.GetExtension(path).ToLowerInvariant() == ".ogg"
                ? (WaveStream)new VorbisWaveReader(path)
                : new AudioFileReader(path);

            _waveOut = new WaveOutEvent();
            _waveOut.Init(_reader);
            _waveOut.Play();
        }
        catch
        {
            StopAndDispose();
        }
    }

    private void StopAndDispose()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _reader?.Dispose();
        _reader = null;
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        StopAndDispose();
        if (_vm?.AudioFilePath is { } path && File.Exists(path))
            PlayFile(path);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => StopAndDispose();

    public void Dispose() => StopAndDispose();
}
