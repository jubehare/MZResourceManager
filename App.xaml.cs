using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MZResourceManager;

public partial class App : Application
{
    private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var sv = FindScrollViewer((DependencyObject)sender);
        if (sv == null || sv.ScrollableWidth <= 0) return;

        sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer sv) return sv;
            var found = FindScrollViewer(child);
            if (found != null) return found;
        }
        return null;
    }
}
