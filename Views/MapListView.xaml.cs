using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MZResourceManager.Views;

public partial class MapListView : UserControl
{
    private Point _panStart;
    private double _panH;
    private double _panV;

    public MapListView() => InitializeComponent();

    private void MapContent_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _panStart = e.GetPosition(MapScrollViewer);
        _panH = MapScrollViewer.HorizontalOffset;
        _panV = MapScrollViewer.VerticalOffset;
        MapContentGrid.CaptureMouse();
        MapContentGrid.Cursor = Cursors.ScrollAll;
    }

    private void MapContent_MouseMove(object sender, MouseEventArgs e)
    {
        if (!MapContentGrid.IsMouseCaptured) return;
        var pos = e.GetPosition(MapScrollViewer);
        MapScrollViewer.ScrollToHorizontalOffset(_panH + (_panStart.X - pos.X));
        MapScrollViewer.ScrollToVerticalOffset(_panV + (_panStart.Y - pos.Y));
    }

    private void MapContent_MouseUp(object sender, MouseButtonEventArgs e)
    {
        MapContentGrid.ReleaseMouseCapture();
        MapContentGrid.Cursor = Cursors.Hand;
    }
}
