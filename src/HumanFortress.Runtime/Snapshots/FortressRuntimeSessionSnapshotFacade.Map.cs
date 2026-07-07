using SadRogue.Primitives;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    internal static ZoneHitData FindZoneAt(FortressRuntimeSession? session, Point worldPosition, int z)
    {
        return FortressRuntimeSnapshotBuilder.FindZoneAt(World(session), worldPosition, z);
    }

    internal static StockpileHitData FindStockpileAt(FortressRuntimeSession? session, Point worldPosition, int z)
    {
        return FortressRuntimeSnapshotBuilder.FindStockpileAt(World(session), worldPosition, z);
    }

    internal static SimulationNavigationPathData FindNavigationDebugPath(
        FortressRuntimeSession? session,
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

    internal static SimulationTileInspectionData BuildTileInspectionSnapshot(FortressRuntimeSession? session, Point tileWorldPosition, int tileZ)
    {
        return FortressRuntimeSnapshotBuilder.BuildTileInspectionSnapshot(
            World(session),
            Geology(session),
            tileWorldPosition,
            tileZ);
    }

    internal static SimulationPlacementPreviewData BuildPlacementPreviewSnapshot(
        FortressRuntimeSession? session,
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode)
    {
        return FortressRuntimeSnapshotBuilder.BuildPlacementPreviewSnapshot(World(session), first, second, z, mode);
    }
}
