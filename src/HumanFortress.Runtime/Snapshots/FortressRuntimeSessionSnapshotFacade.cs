
namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    internal static SimulationBuildCatalogData BuildCatalogSnapshot(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildCatalogSnapshot(Constructions(session));
    }

    internal static SimulationDebugMenuData BuildDebugMenuSnapshot(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildDebugMenuSnapshot(World(session));
    }

    internal static SimulationDebugSpawnData BuildDebugSpawnSnapshot(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildDebugSpawnSnapshot(World(session));
    }

    internal static SimulationWorldAvailabilityData BuildWorldAvailabilitySnapshot(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return FortressRuntimeSnapshotBuilder.BuildWorldAvailabilitySnapshot(World(session));
    }
}
