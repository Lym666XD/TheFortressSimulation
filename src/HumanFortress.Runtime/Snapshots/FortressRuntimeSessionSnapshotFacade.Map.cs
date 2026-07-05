using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    internal static ZoneHitData FindZoneAt(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session, Point worldPosition, int z)
    {
        return FortressRuntimeSnapshotBuilder.FindZoneAt(World(session), worldPosition, z);
    }

    internal static StockpileHitData FindStockpileAt(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session, Point worldPosition, int z)
    {
        return FortressRuntimeSnapshotBuilder.FindStockpileAt(World(session), worldPosition, z);
    }

    internal static SimulationNavigationPathData FindNavigationDebugPath(
        HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session,
        Point start,
        int startZ,
        Point destination,
        int destinationZ)
    {
        return FortressRuntimeSnapshotBuilder.FindNavigationDebugPath(
            Navigation(session),
            NavigationTuning(session),
            start,
            startZ,
            destination,
            destinationZ);
    }

    internal static SimulationTileInspectionData BuildTileInspectionSnapshot(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session, Point tileWorldPosition, int tileZ)
    {
        return FortressRuntimeSnapshotBuilder.BuildTileInspectionSnapshot(
            World(session),
            Geology(session),
            tileWorldPosition,
            tileZ);
    }

    internal static SimulationPlacementPreviewData BuildPlacementPreviewSnapshot(
        HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session,
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode)
    {
        return FortressRuntimeSnapshotBuilder.BuildPlacementPreviewSnapshot(World(session), first, second, z, mode);
    }
}
