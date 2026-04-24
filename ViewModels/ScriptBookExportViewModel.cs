using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MZResourceManager.Models;
using MZResourceManager.Services;
using System.IO;
using System.Windows;

namespace MZResourceManager.ViewModels;

public enum ExportFormat { Html, Docx, Pdf }
public enum ExportTheme { Light, Sepia, Dark, Minimal }

public partial class ScriptBookExportViewModel : ObservableObject
{
    private GameDatabase? _db;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    [NotifyPropertyChangedFor(nameof(ExportLabel))]
    private bool _isExporting;

    [ObservableProperty] private string _statusText = string.Empty;

    [ObservableProperty] private bool _includeMaps = true;
    [ObservableProperty] private bool _includeCommonEvents = true;
    [ObservableProperty] private bool _includeBattleEvents = false;
    [ObservableProperty] private bool _includeActorProfiles = false;
    [ObservableProperty] private bool _includeItemsGlossary = false;
    [ObservableProperty] private bool _includeEmptyPages = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHtml))]
    [NotifyPropertyChangedFor(nameof(IsDocx))]
    [NotifyPropertyChangedFor(nameof(IsPdf))]
    [NotifyPropertyChangedFor(nameof(ExportFilter))]
    [NotifyPropertyChangedFor(nameof(ExportExt))]
    private ExportFormat _format = ExportFormat.Html;

    public bool IsHtml { get => Format == ExportFormat.Html; set { if (value) Format = ExportFormat.Html; } }
    public bool IsDocx { get => Format == ExportFormat.Docx; set { if (value) Format = ExportFormat.Docx; } }
    public bool IsPdf { get => Format == ExportFormat.Pdf; set { if (value) Format = ExportFormat.Pdf; } }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLight))]
    [NotifyPropertyChangedFor(nameof(IsSepia))]
    [NotifyPropertyChangedFor(nameof(IsDark))]
    [NotifyPropertyChangedFor(nameof(IsMinimal))]
    private ExportTheme _theme = ExportTheme.Light;

    public bool IsLight { get => Theme == ExportTheme.Light; set { if (value) Theme = ExportTheme.Light; } }
    public bool IsSepia { get => Theme == ExportTheme.Sepia; set { if (value) Theme = ExportTheme.Sepia; } }
    public bool IsDark { get => Theme == ExportTheme.Dark; set { if (value) Theme = ExportTheme.Dark; } }
    public bool IsMinimal { get => Theme == ExportTheme.Minimal; set { if (value) Theme = ExportTheme.Minimal; } }

    public string ExportFilter => Format switch
    {
        ExportFormat.Docx => "Word Document|*.docx",
        ExportFormat.Pdf => "PDF File|*.pdf",
        _ => "HTML File|*.html",
    };

    public string ExportExt => Format switch
    {
        ExportFormat.Docx => ".docx",
        ExportFormat.Pdf => ".pdf",
        _ => ".html",
    };

    public bool CanExport => _db != null && !IsExporting;
    public string ExportLabel => IsExporting ? "Exporting…" : "Export Script Book";

    public void Initialize(GameDatabase db)
    {
        _db = db;
        StatusText = string.Empty;
        OnPropertyChanged(nameof(CanExport));
    }

    public string GeneratePreviewHtml()
    {
        if (_db == null)
            return "<html><body style='font-family:sans-serif;color:#888;padding:24px'><p>Load a project first.</p></body></html>";
        return ScriptBookBuilder.BuildSimplePreviewHtml(_db, CaptureOptions());
    }


    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        if (_db == null) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export Script Book",
            FileName = $"{_db.GameTitle}_ScriptBook{ExportExt}",
            DefaultExt = ExportExt,
            Filter = ExportFilter,
        };
        if (dlg.ShowDialog() != true) return;

        IsExporting = true;
        StatusText = "Exporting…";

        try
        {
            var opts = CaptureOptions();
            var db = _db;
            var path = dlg.FileName;

            await Task.Run(() => ScriptBookBuilder.Export(path, db, opts));

            StatusText = $"Exported \u2192 {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsExporting = false;
        }
    }

    private ExportOptions CaptureOptions() => new(
        IncludeMaps, IncludeCommonEvents, IncludeBattleEvents,
        IncludeActorProfiles, IncludeItemsGlossary, IncludeEmptyPages,
        Format, Theme);
}
