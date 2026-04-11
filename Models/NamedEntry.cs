namespace MZResourceManager.Models;

public class NamedEntry
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public override string ToString() => $"{Id:D4} {Name}";
}
