namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct SimulationUiOverlayFrameData(
    SimulationBuildCatalogData BuildCatalog,
    SimulationJobsDebugData? Jobs,
    SimulationWorkshopDebugData Workshops,
    SimulationStockpileOverlayData StockpileOverlay,
    SimulationStockpileDetailData? StockpileDetail,
    SimulationZoneOverlayData ZoneOverlay,
    SimulationZoneDetailData? ZoneDetail,
    SimulationManagementDrawerData? ManagementDrawer,
    SimulationWorkDrawerData? WorkDrawer,
    SimulationDebugMenuData? DebugMenu);
