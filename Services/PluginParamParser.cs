using System.IO;
namespace MZResourceManager.Services;

public static class PluginParamParser
{
    public static readonly HashSet<string> DbTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "variable", "switch", "common_event",
        "item", "weapon", "armor", "skill", "state",
        "actor", "class", "enemy", "troop",
        "animation", "tileset", "map",
    };

    public static Dictionary<string, string> ParseParamTypes(string filePath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string? currentParam = null;

            foreach (var rawLine in File.ReadLines(filePath))
            {
                var line = rawLine.AsSpan().TrimStart();
                if (line.StartsWith("*")) line = line[1..].TrimStart();

                if (line.StartsWith("@param ", StringComparison.OrdinalIgnoreCase))
                {
                    currentParam = line[7..].Trim().ToString();
                }
                else if (line.StartsWith("@type ", StringComparison.OrdinalIgnoreCase)
                         && currentParam != null
                         && !result.ContainsKey(currentParam))
                {
                    var type = line[6..].Trim().ToString().ToLowerInvariant();
                    var baseType = type.TrimEnd('[', ']');
                    if (DbTypes.Contains(baseType))
                        result[currentParam] = type;

                    currentParam = null;
                }
            }
        }
        catch { }

        return result;
    }
}
