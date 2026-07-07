using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    internal static SimulationBuildCatalogData BuildCatalogSnapshot(FortressRuntimeSession? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildCatalogSnapshot(Constructions(session));
    }

    internal static SimulationDebugMenuData BuildDebugMenuSnapshot(FortressRuntimeSession? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildDebugMenuSnapshot(World(session));
    }

    internal static SimulationDebugSpawnData BuildDebugSpawnSnapshot(FortressRuntimeSession? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildDebugSpawnSnapshot(World(session));
    }

    internal static SimulationWorldAvailabilityData BuildWorldAvailabilitySnapshot(FortressRuntimeSession? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildWorldAvailabilitySnapshot(World(session));
    }
}
