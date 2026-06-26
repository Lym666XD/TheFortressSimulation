using HumanFortress.Content.Definitions;
using HumanFortress.Content.Loading;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

/// <summary>
/// Loads data-backed world content for an active fortress simulation session.
/// </summary>
internal static partial class SimulationWorldContentLoader
{
    internal static FortressRuntimeContentSnapshot LoadCoreContent(
        World world,
        string baseDir,
        bool strictContent = false,
        bool treatWarningsAsErrors = false,
        Action<string>? log = null,
        Action<FortressContentLoadResult>? logContentIssues = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        var loadedContent = FortressContentLoader.Load(baseDir);
        logContentIssues?.Invoke(loadedContent);
        if (strictContent)
        {
            loadedContent.ThrowIfInvalid(treatWarningsAsErrors);
        }

        if (loadedContent.CoreCatalogs == null)
        {
            log?.Invoke("[GameStateManager] WARNING: Data directory not found. Tried:");
            log?.Invoke($"  - {loadedContent.CoreDataPath.PublishedPath}");
            log?.Invoke($"  - {loadedContent.CoreDataPath.DevelopmentPath}");
            return FortressRuntimeContentSnapshotLoader.CaptureLoaded();
        }

        log?.Invoke($"[GameStateManager] Loading core content catalogs from {loadedContent.CoreDataPath.ResolvedPath}");
        var content = loadedContent.CoreCatalogs;

        LogCreatureDefinitions(content.Creatures, log);
        world.Creatures.SetDefinitionCatalog(content.Creatures.Catalog);

        world.Items.SetDependencies(world);
        LogItemDefinitions(content.Items, log);
        world.Items.SetDefinitionCatalog(content.Items.Catalog);

        var runtimeContent = FortressRuntimeContentSnapshotLoader.ApplyCoreData(content.CoreData);

        foreach (var zoneData in runtimeContent.ZoneDefinitions)
        {
            world.Zones.Manager.RegisterDefinition(zoneData);
        }

        log?.Invoke($"[GameStateManager] Loaded {world.Creatures.DefinitionCount} creatures, {world.Items.DefinitionCount} items, {world.Zones.Manager.GetAllDefinitions().Count()} zone definitions");

        LogCoreDataRegistries(content.CoreData, log);
        return runtimeContent;
    }

}
