using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime;

internal interface IFortressRuntimeSessionSnapshotPort
{
    SimulationStatus SimulationStatus { get; }

    SimulationDebugMenuData GetDebugMenuData();
    SimulationDebugSpawnData GetDebugSpawnData();
    ZoneHitData FindZoneAt(RuntimePoint worldPosition, int z);
    StockpileHitData FindStockpileAt(RuntimePoint worldPosition, int z);

    SimulationNavigationPathData FindNavigationDebugPath(
        RuntimePoint start,
        int startZ,
        RuntimePoint destination,
        int destinationZ);

    SimulationTileInspectionData GetTileInspectionData(RuntimePoint tileWorldPosition, int tileZ);
    WorkforceDebugData GetWorkforceInputData();
    SimulationWorkshopDebugData GetWorkshopDebugData();
    WorkshopSummaryView? GetWorkshopPanelData(Guid workshopId);

    SimulationUiOverlayFrameData GetUiOverlayFrameData(
        RuntimeViewportGeometry viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId);

    SimulationFrameRenderData GetFrameRenderData(
        bool includeMapViewport,
        RuntimeViewportGeometry viewport,
        RuntimePoint cursorPosition,
        int cursorGlyph,
        SimulationNavigationOverlayMode navigationMode,
        RuntimePoint? selectedNavigationTarget,
        RuntimePoint tileInspectionWorldPosition,
        int tileInspectionZ);

    SimulationPlacementPreviewData GetPlacementPreviewData(
        RuntimePoint first,
        RuntimePoint second,
        int z,
        SimulationPlacementPreviewMode mode);
}
