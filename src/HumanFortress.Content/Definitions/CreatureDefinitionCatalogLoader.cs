using System.Text.Json;
using HumanFortress.Contracts.Simulation.Creatures;

namespace HumanFortress.Content.Definitions;

internal sealed class CreatureDefinitionCatalogLoadResult
{
    internal CreatureDefinitionCatalogLoadResult(
        CreatureDefinitionCatalogStore catalog,
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

    internal CreatureDefinitionCatalogStore Catalog { get; }
    internal int LoadedCount { get; }
    internal int FileCount { get; }
    internal int ErrorCount { get; }
    internal IReadOnlyList<string> Messages { get; }
}

internal static partial class CreatureDefinitionCatalogLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    internal static CreatureDefinitionCatalogLoadResult Load(string dataPath)
    {
        var creaturesPath = Path.Combine(dataPath, "creatures");
        var messages = new List<string>();
        var definitions = new List<CreatureDefinition>();

        if (!Directory.Exists(creaturesPath))
        {
            messages.Add($"[CreatureManager] WARNING: Creatures directory not found: {creaturesPath}");
            return new CreatureDefinitionCatalogLoadResult(CreatureDefinitionCatalogStore.Empty, 0, 0, 0, messages);
        }

        var files = Directory.GetFiles(creaturesPath, "*.json");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        int loaded = 0;
        int failed = 0;
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var defs = JsonSerializer.Deserialize<List<CreatureDefinition>>(json, JsonOptions);
                if (defs == null)
                {
                    continue;
                }

                foreach (var def in defs)
                {
                    try
                    {
                        ValidateDefinition(def);
                        if (!seenIds.Add(def.Id))
                        {
                            messages.Add($"[CreatureManager] ERROR: Duplicate or case-ambiguous definition '{def.Id}' in {Path.GetFileName(file)}");
                            failed++;
                            continue;
                        }
                        definitions.Add(def);
                        loaded++;
                    }
                    catch (Exception ex)
                    {
                        messages.Add($"[CreatureManager] ERROR: Invalid definition '{def.Id}' in {Path.GetFileName(file)}: {ex.Message}");
                        failed++;
                    }
                }
            }
            catch (Exception ex)
            {
                messages.Add($"[CreatureManager] ERROR: Failed to load {Path.GetFileName(file)}: {ex.Message}");
                failed++;
            }
        }

        return new CreatureDefinitionCatalogLoadResult(
            CreatureDefinitionCatalogStore.FromDefinitions(definitions),
            loaded,
            files.Length,
            failed,
            messages);
    }

}
