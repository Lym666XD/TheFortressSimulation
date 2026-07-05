using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Runtime.Geometry;
using HumanFortress.Runtime.Snapshots;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    ZoneHitData IFortressRuntimeSessionSnapshotPort.FindZoneAt(RuntimePoint worldPosition, int z)
    {
        return FortressRuntimeSessionSnapshotFacade.FindZoneAt(_runtimeSession, worldPosition.ToSadRoguePoint(), z);
    }

    StockpileHitData IFortressRuntimeSessionSnapshotPort.FindStockpileAt(RuntimePoint worldPosition, int z)
    {
        return FortressRuntimeSessionSnapshotFacade.FindStockpileAt(_runtimeSession, worldPosition.ToSadRoguePoint(), z);
    }

    SimulationNavigationPathData IFortressRuntimeSessionSnapshotPort.FindNavigationDebugPath(
        RuntimePoint start,
        int startZ,
        RuntimePoint destination,
        int destinationZ)
    {
        return FortressRuntimeSessionSnapshotFacade.FindNavigationDebugPath(
            _runtimeSession,
            start.ToSadRoguePoint(),
            startZ,
            destination.ToSadRoguePoint(),
            destinationZ);
    }

    SimulationTileInspectionData IFortressRuntimeSessionSnapshotPort.GetTileInspectionData(
        RuntimePoint tileWorldPosition,
        int tileZ)
    {
        return FortressRuntimeSessionSnapshotFacade.BuildTileInspectionSnapshot(
            _runtimeSession,
            tileWorldPosition.ToSadRoguePoint(),
            tileZ);
    }

    SimulationPlacementPreviewData IFortressRuntimeSessionReadPort.GetPlacementPreviewData(
        RuntimePoint first,
        RuntimePoint second,
        int z,
        SimulationPlacementPreviewMode mode)
    {
        return FortressRuntimeSessionSnapshotFacade.BuildPlacementPreviewSnapshot(
            _runtimeSession,
            first.ToSadRoguePoint(),
            second.ToSadRoguePoint(),
            z,
            mode);
    }
}
