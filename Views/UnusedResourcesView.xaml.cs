using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MZResourceManager.ViewModels;
using NAudio.Vorbis;
using NAudio.Wave;

namespace MZResourceManager.Views;

public partial class UnusedResourcesView : UserControl, IDisposable
{
    private UnusedResourcesViewModel? _vm;
    private WaveOutEvent? _waveOut;
    private WaveStream? _reader;

    public UnusedResourcesView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as UnusedResourcesViewModel;

        if (_vm != null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UnusedResourcesViewModel.AudioPreviewFilePath))
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
        catch { StopAndDispose(); }
    }

    private void StopAndDispose()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _reader?.Dispose();
        _reader = null;
    }

    private void AudioPlay_Click(object sender, RoutedEventArgs e)
    {
        StopAndDispose();
        if (_vm?.AudioPreviewFilePath is { } path && File.Exists(path))
            PlayFile(path);
    }

    private void AudioStop_Click(object sender, RoutedEventArgs e) => StopAndDispose();

    public void Dispose() => StopAndDispose();
}
