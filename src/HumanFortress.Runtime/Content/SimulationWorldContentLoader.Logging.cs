using HumanFortress.Content.Definitions;
using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Runtime.Content;

internal static partial class SimulationWorldContentLoader
{
    private static void LogCreatureDefinitions(CreatureDefinitionCatalogLoadResult result, Action<string>? log)
    {
        foreach (var message in result.Messages)
        {
            log?.Invoke(message);
        }

        log?.Invoke($"[CreatureManager] Loaded {result.LoadedCount} creature definitions from {result.FileCount} files ({result.ErrorCount} errors)");
    }

    private static void LogItemDefinitions(ItemDefinitionCatalogLoadResult result, Action<string>? log)
    {
        foreach (var message in result.Messages)
        {
            log?.Invoke(message);
        }

        var availableKinds = result.Catalog.GetAvailableKinds().ToArray();
        log?.Invoke($"[ItemManager] Loaded {result.LoadedCount} item definitions from {result.FileCount} files ({result.ErrorCount} errors)");
        log?.Invoke($"[ItemManager] Indexed {availableKinds.Length} kinds: {string.Join(", ", availableKinds)}");
    }

    private static void LogCoreDataRegistries(CoreDataLoadResult result, Action<string>? log)
    {
        try
        {
            foreach (var message in result.Constructions.Messages)
            {
                log?.Invoke($"[CONSTR.REG] {message}");
            }

            log?.Invoke(
                $"[CONSTR.REG] loaded={result.Constructions.LoadedCount} categories=[{string.Join(',', result.Constructions.Categories)}] errors={result.Constructions.ErrorCount} duplicates_skipped={result.Constructions.DuplicatesSkipped}");

            foreach (var message in result.Recipes.Messages)
            {
                log?.Invoke($"[RECIPES] {message}");
            }

            log?.Invoke($"[RECIPES] loaded={result.Recipes.LoadedCount} errors={result.Recipes.ErrorCount}");
        }
        catch (Exception ex)
        {
            log?.Invoke($"[CONTENT.DATA] ERROR: failed loading core data registries: {ex.Message}");
        }
    }
}
