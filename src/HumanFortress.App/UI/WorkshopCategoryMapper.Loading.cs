using System.Text.Json;
using HumanFortress.App.Content;

namespace HumanFortress.App.UI;

internal static partial class WorkshopCategoryMapper
{
    private static void EnsureLoaded()
    {
        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;
            try
            {
                string baseDir = AppContext.BaseDirectory;
                var registryFile = AppContentFileLocator.ResolveRegistryFile(baseDir, "ui.workshop_categories.json");
                _map = registryFile.ResolvedPath == null
                    ? GetFallback()
                    : LoadMapping(registryFile.ResolvedPath);
            }
            catch
            {
                _map = GetFallback();
            }
            finally { _loaded = true; }
        }
    }

    private static Dictionary<string, string[]> LoadMapping(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var dict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.NameEquals("_comment")) continue;
            if (prop.Value.ValueKind != JsonValueKind.Array) continue;

            var tags = prop.Value.EnumerateArray()
                .Where(element => element.ValueKind == JsonValueKind.String)
                .Select(element => element.GetString() ?? string.Empty)
                .Where(tag => !string.IsNullOrEmpty(tag))
                .ToArray();
            dict[prop.Name] = tags;
        }

        return dict;
    }
}
