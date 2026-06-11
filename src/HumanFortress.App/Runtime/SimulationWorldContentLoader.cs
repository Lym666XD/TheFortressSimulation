using HumanFortress.Simulation.World;
using DataContentRegistry = HumanFortress.Core.Content.ContentRegistry;
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

        EnsureContentRegistriesLoaded(baseDir);

        var dataPath = TryFindCoreDataPath(baseDir, out var publishedPath, out var developmentPath);
        if (dataPath == null)
        {
            Logger.Log("[GameStateManager] WARNING: Data directory not found. Tried:");
            Logger.Log($"  - {publishedPath}");
            Logger.Log($"  - {developmentPath}");
            return;
        }

        Logger.Log($"[GameStateManager] Loading creature and item definitions from {dataPath}");
        world.Creatures.LoadDefinitions(dataPath);

        world.Items.SetDependencies(world, RuntimeContentRegistry.Instance);
        world.Items.LoadDefinitions(dataPath);

        foreach (var zoneData in RuntimeContentRegistry.Instance.Zones.Values)
        {
            world.Zones.Manager.RegisterDefinition(zoneData);
        }

        Logger.Log($"[GameStateManager] Loaded {world.Creatures.DefinitionCount} creatures, {world.Items.DefinitionCount} items, {world.Zones.Manager.GetAllDefinitions().Count()} zone definitions");

        LoadCoreDataRegistries(dataPath);
    }

    private static void EnsureContentRegistriesLoaded(string baseDir)
    {
        if (DataContentRegistry.Instance.Materials.Count > 0 && RuntimeContentRegistry.Instance.IsLoaded)
        {
            return;
        }

        var contentPath = TryFindContentPath(baseDir, out var publishedPath, out var developmentPath);
        if (contentPath == null)
        {
            Logger.Warning(
                "Content.Registry",
                $"[ContentLoadCoordinator] Content directory not found. Tried: {publishedPath}; {developmentPath}");
            return;
        }

        var result = HumanFortress.Core.Content.ContentLoadCoordinator.Load(contentPath);
        if (!result.StructuredLoaded)
        {
            Logger.Warning(
                "Content.Registry",
                $"[ContentLoadCoordinator] Structured registry unavailable: {result.StructuredFailureMessage ?? "unknown error"}");
        }
    }

    private static string? TryFindContentPath(string baseDir, out string publishedPath, out string developmentPath)
    {
        publishedPath = Path.Combine(baseDir, "content");
        if (Directory.Exists(publishedPath))
        {
            developmentPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "content"));
            return publishedPath;
        }

        developmentPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "content"));
        return Directory.Exists(developmentPath) ? developmentPath : null;
    }

    private static string? TryFindCoreDataPath(string baseDir, out string publishedPath, out string developmentPath)
    {
        publishedPath = Path.Combine(baseDir, "data", "core");
        if (Directory.Exists(publishedPath))
        {
            developmentPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "data", "core"));
            return publishedPath;
        }

        developmentPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "data", "core"));
        return Directory.Exists(developmentPath) ? developmentPath : null;
    }

    private static void LoadCoreDataRegistries(string dataPath)
    {
        try
        {
            var result = RuntimeContentRegistry.Instance.LoadCoreData(dataPath);
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
