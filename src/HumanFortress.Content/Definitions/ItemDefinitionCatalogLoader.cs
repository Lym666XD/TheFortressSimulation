using System.Text.Json;
using System.Text.Json.Serialization;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Simulation.Items;

namespace HumanFortress.Content.Definitions;

internal sealed class ItemDefinitionCatalogLoadResult
{
    internal ItemDefinitionCatalogLoadResult(
        ItemDefinitionCatalogStore catalog,
        int loadedCount,
        int fileCount,
        int errorCount,
        IReadOnlyList<string> messages)
    {
        Catalog = catalog;
        LoadedCount = loadedCount;
        FileCount = fileCount;
        ErrorCount = errorCount;
        Messages = messages;
    }

    internal ItemDefinitionCatalogStore Catalog { get; }
    internal int LoadedCount { get; }
    internal int FileCount { get; }
    internal int ErrorCount { get; }
    internal IReadOnlyList<string> Messages { get; }
}

internal static partial class ItemDefinitionCatalogLoader
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    internal static ItemDefinitionCatalogLoadResult Load(string dataPath)
    {
        var itemsPath = Path.Combine(dataPath, "items");
        var messages = new List<string>();
        var definitions = new List<ItemDefinition>();

        if (!Directory.Exists(itemsPath))
        {
            messages.Add($"[ItemManager] WARNING: Items directory not found: {itemsPath}");
            return new ItemDefinitionCatalogLoadResult(ItemDefinitionCatalogStore.Empty, 0, 0, 0, messages);
        }

        var files = Directory.GetFiles(itemsPath, "*.json");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        int loaded = 0;
        int failed = 0;

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var defs = ParseDefinitions(file, json, messages, ref failed);
                if (defs == null)
                {
                    continue;
                }

                foreach (var def in defs)
                {
                    try
                    {
                        NormalizeDefinition(def);
                        EnrichGenericResourceName(def, messages);
                        ValidateDefinition(def, messages);
                        definitions.Add(def);
                        loaded++;
                    }
                    catch (Exception ex)
                    {
                        messages.Add($"[ItemManager] ERROR: Invalid definition '{def.Id}' in {Path.GetFileName(file)}: {ex.Message}");
                        failed++;
                    }
                }
            }
            catch (Exception ex)
            {
                messages.Add($"[ItemManager] ERROR: Failed to load {Path.GetFileName(file)}: {ex.Message}");
                failed++;
            }
        }

        return new ItemDefinitionCatalogLoadResult(
            ItemDefinitionCatalogStore.FromDefinitions(definitions),
            loaded,
            files.Length,
            failed,
            messages);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        return options;
    }
}
