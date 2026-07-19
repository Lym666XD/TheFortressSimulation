using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal ZoneHitData FindZoneAt(Point worldPosition, int z)
    {
        if (!TryGetCommittedFrame(out var committed)
            || committed.Request.Viewport.CurrentZ != z)
        {
            return ZoneHitData.Empty;
        }

        var cell = committed.Frame.UiOverlay.ZoneOverlay.Cells.FirstOrDefault(candidate =>
            candidate.X == worldPosition.X && candidate.Y == worldPosition.Y);
        return cell.ZoneId <= 0
            ? ZoneHitData.Empty
            : new ZoneHitData(true, cell.ZoneId, worldPosition.X, worldPosition.Y, z);
    }

    internal StockpileHitData FindStockpileAt(Point worldPosition, int z)
    {
        if (!TryGetCommittedFrame(out var committed)
            || committed.Request.Viewport.CurrentZ != z)
        {
            return StockpileHitData.Empty;
        }

        var cell = committed.Frame.UiOverlay.StockpileOverlay.Cells.FirstOrDefault(candidate =>
            candidate.X == worldPosition.X && candidate.Y == worldPosition.Y);
        return cell.ZoneId <= 0
            ? StockpileHitData.Empty
            : new StockpileHitData(
                true,
                cell.ZoneId,
                new SnapshotPoint(worldPosition.X, worldPosition.Y));
    }

    internal SimulationTileInspectionData GetTileInspectionData(Point tileWorldPosition, int tileZ)
    {
        if (!TryGetCommittedFrame(out var committed)
            || committed.Request.TileInspectionZ != tileZ
            || committed.Request.TileInspectionWorldPosition.X != tileWorldPosition.X
            || committed.Request.TileInspectionWorldPosition.Y != tileWorldPosition.Y)
        {
            return SimulationTileInspectionData.Empty;
        }

        return committed.Frame.FrameRender.TileInspection;
    }

    internal SimulationWorkshopDebugData GetWorkshopDebugData()
    {
        return TryGetCommittedFrame(out var committed)
            ? committed.Frame.UiOverlay.Workshops
            : new SimulationWorkshopDebugData(Array.Empty<WorkshopSummaryView>(), 0, 0, 0);
    }

    ZoneHitData IFortressRuntimeMapInspectionAccess.FindZoneAt(Point worldPosition, int z) =>
        FindZoneAt(worldPosition, z);

    ZoneHitData IFortressRuntimePlacementQueryAccess.FindZoneAt(Point worldPosition, int z) =>
        FindZoneAt(worldPosition, z);

    StockpileHitData IFortressRuntimeMapInspectionAccess.FindStockpileAt(Point worldPosition, int z) =>
        FindStockpileAt(worldPosition, z);

    StockpileHitData IFortressRuntimePlacementQueryAccess.FindStockpileAt(Point worldPosition, int z) =>
        FindStockpileAt(worldPosition, z);

    SimulationTileInspectionData IFortressRuntimeMapInspectionAccess.GetTileInspectionData(Point tileWorldPosition, int tileZ) =>
        GetTileInspectionData(tileWorldPosition, tileZ);

    SimulationWorkshopDebugData IFortressRuntimeMapInspectionAccess.GetWorkshopDebugData() => GetWorkshopDebugData();
}
