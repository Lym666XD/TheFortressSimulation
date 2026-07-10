namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct SimulationFrameRenderData(
    SimulationMapViewportData MapViewport,
    SimulationNavigationOverlayData NavigationOverlay,
    SimulationTileInspectionData TileInspection,
    SimulationSnapshotMetadata Metadata = default,
    SimulationSnapshotPublicationData Publication = default,
    SimulationSnapshotPresenterFrameData PresenterFrame = default);
