using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationBuildCatalogData BuildCatalogSnapshot(
        IConstructionCatalog? constructions,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? workshopCategoryTags = null)
    {
        return BuildCatalogSnapshotBuilder.Build(constructions, workshopCategoryTags);
    }

    internal static SimulationDebugMenuData BuildDebugMenuSnapshot(World? world)
    {
        return DebugMenuSnapshotBuilder.Build(world);
    }

    internal static SimulationZoneCatalogData BuildZoneCatalogSnapshot(World? world)
    {
        return ZoneCatalogSnapshotBuilder.Build(world);
    }

    internal static SimulationDebugSpawnData BuildDebugSpawnSnapshot(World? world)
    {
        return DebugMenuSnapshotBuilder.BuildSpawnData(world);
    }

    internal static SimulationManagementDrawerData BuildManagementDrawerSnapshot(World? world)
    {
        return ManagementDrawerSnapshotBuilder.Build(world);
    }

    internal static SimulationWorldAvailabilityData BuildWorldAvailabilitySnapshot(World? world)
    {
        return world == null
            ? SimulationWorldAvailabilityData.Empty
            : new SimulationWorldAvailabilityData(
                true,
                new RuntimeWorldBounds(
                    0,
                    0,
                    world.SizeInTiles,
                    world.SizeInTiles,
                    0,
                    world.MaxZ));
    }
}
