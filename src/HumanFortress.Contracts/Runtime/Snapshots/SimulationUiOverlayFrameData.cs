namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct SimulationUiOverlayFrameData(
    SimulationBuildCatalogData BuildCatalog,
    SimulationJobsDebugData? Jobs,
    SimulationWorkshopDebugData Workshops,
    SimulationStockpilePresetMenuData StockpilePresets,
    SimulationStockpileOverlayData StockpileOverlay,
    SimulationStockpileDetailData? StockpileDetail,
    SimulationZoneOverlayData ZoneOverlay,
    SimulationZoneDetailData? ZoneDetail,
    SimulationManagementDrawerData? ManagementDrawer,
    SimulationWorkDrawerData? WorkDrawer,
    SimulationDebugMenuData? DebugMenu,
    SimulationSnapshotMetadata Metadata = default);
