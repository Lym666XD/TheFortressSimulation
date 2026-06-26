using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationBuildCatalogData BuildCatalogSnapshot(IConstructionCatalog? constructions)
    {
        return BuildCatalogSnapshotBuilder.Build(constructions);
    }

    internal static SimulationDebugMenuData BuildDebugMenuSnapshot(World? world)
    {
        return DebugMenuSnapshotBuilder.Build(world);
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
        return new SimulationWorldAvailabilityData(world != null);
    }
}
