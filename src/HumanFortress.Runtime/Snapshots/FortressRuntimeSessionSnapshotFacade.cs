using Session = HumanFortress.Runtime.SimulationRuntimeSession<HumanFortress.Runtime.SimulationRuntimeHost<HumanFortress.Runtime.SimulationRuntimeSystems>>;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    internal static SimulationBuildCatalogData BuildCatalogSnapshot(Session? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildCatalogSnapshot(Constructions(session));
    }

    internal static SimulationDebugMenuData BuildDebugMenuSnapshot(Session? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildDebugMenuSnapshot(World(session));
    }

    internal static SimulationDebugSpawnData BuildDebugSpawnSnapshot(Session? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildDebugSpawnSnapshot(World(session));
    }

    internal static SimulationWorldAvailabilityData BuildWorldAvailabilitySnapshot(Session? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildWorldAvailabilitySnapshot(World(session));
    }
}
