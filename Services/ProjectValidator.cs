using System.IO;

namespace MZResourceManager.Services;

public static class ProjectValidator
{
    public static string? GetValidationError(string folder)
    {
        if (!Directory.Exists(folder))
            return "Folder does not exist.";

        string[] required = [
            Path.Combine(folder, "js", "rmmz_core.js"),
            Path.Combine(folder, "data", "System.json"),
            Path.Combine(folder, "data", "MapInfos.json"),
            Path.Combine(folder, "data", "Items.json"),
        ];

        foreach (var path in required)
            if (!File.Exists(path))
                return "Not a valid MZ project folder.";

        return null;
    }
}
