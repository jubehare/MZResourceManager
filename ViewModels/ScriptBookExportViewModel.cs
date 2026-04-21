using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MZResourceManager.Models;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace MZResourceManager.ViewModels;

public partial class ScriptBookExportViewModel : ObservableObject
{
    private GameDatabase? _db;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExportLabel))]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    private bool _isExporting;

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _includeMaps = true;
    [ObservableProperty] private bool _includeCommonEvents = true;
    [ObservableProperty] private bool _includeEmptyPages = false;

    public bool CanExport => _db != null && !IsExporting;
    public string ExportLabel => IsExporting ? "Exporting…" : "Export Script Book";

    public void Initialize(GameDatabase db)
    {
        _db = db;
        StatusText = string.Empty;
        OnPropertyChanged(nameof(CanExport));
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        if (_db == null) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export Script Book",
            FileName = $"{_db.GameTitle}_ScriptBook.txt",
            DefaultExt = ".txt",
            Filter = "Text File|*.txt"
        };
        if (dlg.ShowDialog() != true) return;

        IsExporting = true;
        StatusText = "Exporting…";

        try
        {
            var db = _db;
            var includeMaps = IncludeMaps;
            var includeCommon = IncludeCommonEvents;
            var includeEmpty = IncludeEmptyPages;
            var path = dlg.FileName;

            await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# Script Book — {db.GameTitle}");
                sb.AppendLine($"# Generated {DateTime.Now:yyyy-MM-dd HH:mm}");
                sb.AppendLine();

                if (includeMaps)
                {
                    sb.AppendLine("═══════════════════════════════════════════════════════════");
                    sb.AppendLine("  MAP EVENTS");
                    sb.AppendLine("═══════════════════════════════════════════════════════════");
                    sb.AppendLine();

                    foreach (var info in db.MapInfos.OrderBy(m => m.Id))
                    {
                        if (!db.Maps.TryGetValue(info.Id, out var map)) continue;
                        var events = map.Events?.Where(e => e != null).ToList() ?? [];
                        if (events.Count == 0) continue;

                        sb.AppendLine($"┌─ MAP {info.Id:D3}: {info.Name}");
                        sb.AppendLine();

                        foreach (var ev in events)
                        {
                            var pages = ev.Pages?.Where(p => p?.List != null).ToList() ?? [];
                            if (!includeEmpty)
                                pages = pages.Where(p => p!.List!.Any(c => c.Code != 0)).ToList();
                            if (pages.Count == 0) continue;

                            sb.AppendLine($"  ◆ Event {ev.Id:D3}: {ev.Name}");

                            for (int pi = 0; pi < pages.Count; pi++)
                            {
                                sb.AppendLine($"    ▸ Page {pi + 1}");
                                AppendCommands(sb, pages[pi]!.List!, "      ");
                            }
                            sb.AppendLine();
                        }
                        sb.AppendLine();
                    }
                }

                if (includeCommon)
                {
                    sb.AppendLine("═══════════════════════════════════════════════════════════");
                    sb.AppendLine("  COMMON EVENTS");
                    sb.AppendLine("═══════════════════════════════════════════════════════════");
                    sb.AppendLine();

                    foreach (var ce in db.CommonEvents.OrderBy(c => c.Id))
                    {
                        var cmds = ce.List?.Where(c => c.Code != 0).ToList() ?? [];
                        if (!includeEmpty && cmds.Count == 0) continue;

                        sb.AppendLine($"  ◆ Common Event {ce.Id:D3}: {ce.Name}");
                        AppendCommands(sb, ce.List ?? [], "    ");
                        sb.AppendLine();
                    }
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            });

            StatusText = $"Exported to {Path.GetFileName(dlg.FileName)}";
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

    private static void AppendCommands(StringBuilder sb, IList<EventCommand> cmds, string indent)
    {
        foreach (var cmd in cmds)
        {
            var line = FormatCommand(cmd);
            if (line != null)
                sb.AppendLine($"{indent}{line}");
        }
    }

    private static string? FormatCommand(EventCommand cmd) => cmd.Code switch
    {
        0 => null,
        101 => $"[Face] {Str(cmd, 0)} / {Str(cmd, 2)}",
        401 => $"  \"{Str(cmd, 0)}\"",
        102 => $"[Show Choices] {string.Join(" / ", cmd.Parameters.Select(p => p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : ""))}",
        402 => $"  ▷ Choice: {Str(cmd, 1)}",
        103 => $"[Input Number] → var #{Int(cmd, 0)}",
        111 => $"[If] {ConditionLabel(cmd)}",
        121 => $"[Control Switches] #{Int(cmd, 0)}–#{Int(cmd, 1)} = {(Int(cmd, 2) == 0 ? "ON" : "OFF")}",
        122 => $"[Control Variables] #{Int(cmd, 0)}–#{Int(cmd, 1)} {VarOp(cmd)}",
        123 => $"[Control Self Switch] {Str(cmd, 0)} = {Str(cmd, 1)}",
        125 => $"[Change Gold]",
        126 => $"[Change Items] item #{Int(cmd, 0)}",
        129 => $"[Change Party Member] actor #{Int(cmd, 0)}",
        201 => $"[Transfer Player] map #{Int(cmd, 1)}  ({Int(cmd, 2)}, {Int(cmd, 3)})",
        202 => $"[Set Vehicle Location] map #{Int(cmd, 1)}",
        213 => $"[Balloon Icon]",
        214 => $"[Erase Event]",
        221 => $"[Fadeout Screen]",
        222 => $"[Fadein Screen]",
        223 => $"[Tint Screen]",
        224 => $"[Flash Screen]",
        225 => $"[Shake Screen]",
        230 => $"[Wait] {Int(cmd, 0)} frames",
        231 => $"[Show Picture] #{Int(cmd, 0)}: {Str(cmd, 1)}",
        235 => $"[Erase Picture] #{Int(cmd, 0)}",
        241 => $"[Play BGM] {AudioName(cmd, 0)}",
        245 => $"[Play BGS] {AudioName(cmd, 0)}",
        249 => $"[Play ME] {AudioName(cmd, 0)}",
        250 => $"[Play SE] {AudioName(cmd, 0)}",
        261 => $"[Play Movie] {Str(cmd, 0)}",
        301 => $"[Battle Processing] troop #{Int(cmd, 1)}",
        302 => $"[Shop Processing]",
        303 => $"[Name Input Processing]",
        320 => $"[Change Actor Name] actor #{Int(cmd, 0)} → {Str(cmd, 1)}",
        351 => $"[Open Menu Screen]",
        352 => $"[Open Save Screen]",
        353 => $"[Game Over]",
        354 => $"[Return to Title]",
        355 => $"[Script] {Str(cmd, 0)}",
        356 => $"[Plugin Command] {Str(cmd, 0)}",
        _ => $"[#{cmd.Code}]"
    };

    private static string Str(EventCommand c, int i)
    {
        if (i >= c.Parameters.Length) return "";
        var p = c.Parameters[i];
        return p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : p.ToString();
    }

    private static int Int(EventCommand c, int i)
    {
        if (i >= c.Parameters.Length) return 0;
        var p = c.Parameters[i];
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v)) return v;
        return 0;
    }

    private static string AudioName(EventCommand c, int i)
    {
        if (i >= c.Parameters.Length) return "";
        try
        {
            var audio = c.GetAudioParam(i);
            return audio?.Name ?? "";
        }
        catch { return ""; }
    }

    private static string VarOp(EventCommand c) => Int(c, 2) switch
    {
        0 => $"= {Int(c, 3)}",
        1 => $"+= {Int(c, 3)}",
        2 => $"-= {Int(c, 3)}",
        3 => $"*= {Int(c, 3)}",
        4 => $"/= {Int(c, 3)}",
        5 => $"%= {Int(c, 3)}",
        _ => ""
    };

    private static string ConditionLabel(EventCommand c) => Int(c, 0) switch
    {
        0 => $"Switch #{Int(c, 1)} is {(Int(c, 2) == 0 ? "ON" : "OFF")}",
        1 => $"Variable #{Int(c, 1)}",
        2 => $"Self Switch {Str(c, 1)} is {Str(c, 2)}",
        4 => $"Actor #{Int(c, 1)}",
        5 => $"Enemy #{Int(c, 1)}",
        _ => $"type {Int(c, 0)}"
    };
}
