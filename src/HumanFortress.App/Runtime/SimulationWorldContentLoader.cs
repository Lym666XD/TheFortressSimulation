using HumanFortress.Content.Definitions;
using HumanFortress.Content.Loading;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.World;
using RuntimeContentRegistry = HumanFortress.Core.Content.Registry.ContentRegistry;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Loads data-backed world content for an active fortress simulation session.
/// </summary>
internal static class SimulationWorldContentLoader
{
    public static void LoadCoreContent(World world, string baseDir)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        var loadedContent = FortressContentLoader.Load(baseDir);
        FortressContentIssueLogger.LogIssues(loadedContent);

        if (loadedContent.CoreCatalogs == null)
        {
            Logger.Log("[GameStateManager] WARNING: Data directory not found. Tried:");
            Logger.Log($"  - {loadedContent.CoreDataPath.PublishedPath}");
            Logger.Log($"  - {loadedContent.CoreDataPath.DevelopmentPath}");
            return;
        }

        Logger.Log($"[GameStateManager] Loading core content catalogs from {loadedContent.CoreDataPath.ResolvedPath}");
        var content = loadedContent.CoreCatalogs;

        LogCreatureDefinitions(content.Creatures);
        world.Creatures.SetDefinitionCatalog(content.Creatures.Catalog);

        world.Items.SetDependencies(world, RuntimeContentRegistry.Instance);
        LogItemDefinitions(content.Items);
        world.Items.SetDefinitionCatalog(content.Items.Catalog);

        RuntimeContentRegistry.Instance.ApplyCoreData(content.CoreData);

        foreach (var zoneData in RuntimeContentRegistry.Instance.Zones.Values)
        {
            world.Zones.Manager.RegisterDefinition(zoneData);
        }

        Logger.Log($"[GameStateManager] Loaded {world.Creatures.DefinitionCount} creatures, {world.Items.DefinitionCount} items, {world.Zones.Manager.GetAllDefinitions().Count()} zone definitions");

        LogCoreDataRegistries(content.CoreData);
    }

    private static void LogCreatureDefinitions(CreatureDefinitionCatalogLoadResult result)
    {
        foreach (var message in result.Messages)
        {
            Logger.Log(message);
        }

        Logger.Log($"[CreatureManager] Loaded {result.LoadedCount} creature definitions from {result.FileCount} files ({result.ErrorCount} errors)");
    }

    private static void LogItemDefinitions(ItemDefinitionCatalogLoadResult result)
    {
        foreach (var message in result.Messages)
        {
            Logger.Log(message);
        }

        var availableKinds = result.Catalog.GetAvailableKinds().ToArray();
        Logger.Log($"[ItemManager] Loaded {result.LoadedCount} item definitions from {result.FileCount} files ({result.ErrorCount} errors)");
        Logger.Log($"[ItemManager] Indexed {availableKinds.Length} kinds: {string.Join(", ", availableKinds)}");
    }

    private static void LogCoreDataRegistries(CoreDataLoadResult result)
    {
        try
        {
            foreach (var message in result.Constructions.Messages)
            {
                Logger.Log($"[CONSTR.REG] {message}");
            }

            Logger.Log(
                $"[CONSTR.REG] loaded={result.Constructions.LoadedCount} categories=[{string.Join(',', result.Constructions.Categories)}] errors={result.Constructions.ErrorCount} duplicates_skipped={result.Constructions.DuplicatesSkipped}");

            foreach (var message in result.Recipes.Messages)
            {
                Logger.Log($"[RECIPES] {message}");
            }

            Logger.Log($"[RECIPES] loaded={result.Recipes.LoadedCount} errors={result.Recipes.ErrorCount}");
        }
        catch (Exception ex)
        {
            Logger.Log($"[CONTENT.DATA] ERROR: failed loading core data registries: {ex.Message}");
        }
    }

}
