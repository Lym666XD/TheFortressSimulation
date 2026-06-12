using System.Text.Json;
using HumanFortress.Simulation.Creatures;

namespace HumanFortress.Content.Definitions;

public sealed class CreatureDefinitionCatalogLoadResult
{
    public CreatureDefinitionCatalogLoadResult(
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

    public CreatureDefinitionCatalogStore Catalog { get; }
    public int LoadedCount { get; }
    public int FileCount { get; }
    public int ErrorCount { get; }
    public IReadOnlyList<string> Messages { get; }
}

public static class CreatureDefinitionCatalogLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static CreatureDefinitionCatalogLoadResult Load(string dataPath)
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

    private static void ValidateDefinition(CreatureDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.Id))
        {
            throw new ArgumentException("Creature ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(def.Name))
        {
            throw new ArgumentException($"Creature '{def.Id}' has no name");
        }

        if (def.BaseSpeed <= 0)
        {
            throw new ArgumentException($"Creature '{def.Id}' has invalid speed: {def.BaseSpeed}");
        }

        if (def.BaseStrength < 1 || def.BaseStrength > 100)
        {
            throw new ArgumentException($"Creature '{def.Id}' has invalid strength: {def.BaseStrength}");
        }
    }
}
