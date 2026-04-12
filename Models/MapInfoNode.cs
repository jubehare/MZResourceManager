using System.Collections.ObjectModel;

namespace MZResourceManager.Models;

public class MapInfoNode
{
    public MapInfo Info { get; set; } = null!;
    public ObservableCollection<MapInfoNode> Children { get; set; } = [];
    public string Display => $"{Info.Id:D3} {Info.Name}";
}
