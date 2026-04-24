using System.ComponentModel;
using System.Windows.Controls;
using MZResourceManager.ViewModels;

namespace MZResourceManager.Views;

public partial class ScriptBookExportView : UserControl
{
    public ScriptBookExportView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private ScriptBookExportViewModel? _vm;

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as ScriptBookExportViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            RefreshPreview();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ScriptBookExportViewModel.IsExporting)
                           or nameof(ScriptBookExportViewModel.StatusText)
                           or nameof(ScriptBookExportViewModel.CanExport)
                           or nameof(ScriptBookExportViewModel.ExportLabel)
                           or nameof(ScriptBookExportViewModel.ExportFilter)
                           or nameof(ScriptBookExportViewModel.ExportExt))
            return;

        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_vm == null) return;
        try
        {
            var html = _vm.GeneratePreviewHtml();
            PreviewBrowser.NavigateToString(html);
        }
        catch { }
    }
}
