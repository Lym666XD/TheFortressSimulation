using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Checkpoints;

namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct SimulationNavigationPathRequestData(
    RuntimePoint Start,
    int StartZ,
    RuntimePoint Destination,
    int DestinationZ)
{
    public SimulationNavigationPathRequestData Canonicalize(RuntimeWorldBounds worldBounds)
    {
        if (worldBounds.IsEmpty)
            return this;

        return this with
        {
            Start = new RuntimePoint(
                Math.Clamp(Start.X, worldBounds.MinX, worldBounds.MaxXExclusive - 1),
                Math.Clamp(Start.Y, worldBounds.MinY, worldBounds.MaxYExclusive - 1)),
            StartZ = Math.Clamp(StartZ, worldBounds.MinZ, worldBounds.MaxZExclusive - 1),
            Destination = new RuntimePoint(
                Math.Clamp(Destination.X, worldBounds.MinX, worldBounds.MaxXExclusive - 1),
                Math.Clamp(Destination.Y, worldBounds.MinY, worldBounds.MaxYExclusive - 1)),
            DestinationZ = Math.Clamp(
                DestinationZ,
                worldBounds.MinZ,
                worldBounds.MaxZExclusive - 1),
        };
    }
}

public readonly record struct SimulationNavigationPathFrameData(
    SimulationSnapshotMetadata Metadata,
    bool IsAvailable,
    SimulationNavigationPathRequestData? Request,
    SimulationNavigationPathData Path)
{
    public static SimulationNavigationPathFrameData Unavailable { get; } = new(
        default,
        false,
        null,
        SimulationNavigationPathData.Unavailable);
}

public readonly record struct SimulationAppFrameRequestData(
    bool IncludeMapViewport,
    RuntimeViewportGeometry Viewport,
    RuntimePoint CursorPosition,
    int CursorGlyph,
    SimulationNavigationOverlayMode NavigationMode,
    RuntimePoint? SelectedNavigationTarget,
    RuntimePoint TileInspectionWorldPosition,
    int TileInspectionZ,
    bool ShowZoneOverlay,
    bool IncludeManagementDrawer,
    bool IncludeWorkDrawer,
    bool IncludeDebugMenu,
    int? StockpileDetailZoneId,
    int? ZoneDetailId,
    IReadOnlyList<SimulationPlacementPreviewRequestData> PlacementPreviewRequests,
    SimulationNavigationPathRequestData? NavigationPathRequest);

public readonly record struct SimulationAppFrameData(
    bool IsAvailable,
    RuntimeCheckpointIdentityData CheckpointIdentity,
    SimulationFrameRenderData FrameRender,
    SimulationUiOverlayFrameData UiOverlay,
    SimulationPlacementPreviewFrameData PlacementPreviews,
    SimulationNavigationPathFrameData NavigationPath,
    SimulationStatus SimulationStatus)
{
    public static SimulationAppFrameData Unavailable { get; } = new(
        false,
        default,
        default,
        default,
        SimulationPlacementPreviewFrameData.Empty,
        SimulationNavigationPathFrameData.Unavailable,
        default);
}
