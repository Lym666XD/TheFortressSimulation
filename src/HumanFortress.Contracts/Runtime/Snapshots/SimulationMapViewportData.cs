using HumanFortress.Contracts.Runtime;

namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct MapViewportCellView(
    int ScreenX,
    int ScreenY,
    int Glyph,
    SnapshotColor Color);

public readonly record struct MapViewportRowDeltaView(
    int ScreenY,
    string PayloadHash,
    string PayloadHashAlgorithm,
    IReadOnlyList<MapViewportCellView> Cells);

public readonly record struct MapViewportRegionDeltaView(
    int RegionX,
    int RegionY,
    int ScreenX,
    int ScreenY,
    int Width,
    int Height,
    string PayloadHash,
    string PayloadHashAlgorithm,
    IReadOnlyList<MapViewportCellView> Cells);

public static class SimulationMapViewportDeltaSchema
{
    public const int CurrentVersion = 3;
    public const int RegionSize = 8;
}

public readonly record struct SimulationMapViewportDeltaData(
    int SchemaVersion,
    bool IsAvailable,
    bool CanApplyToBase,
    string PayloadHash,
    string PayloadHashAlgorithm,
    string? BasePayloadHash,
    IReadOnlyList<MapViewportCellView> ChangedCells,
    IReadOnlyList<MapViewportRowDeltaView> ChangedRows,
    IReadOnlyList<MapViewportRegionDeltaView> ChangedRegions)
{
    public static SimulationMapViewportDeltaData Unavailable { get; } = new(
        SimulationMapViewportDeltaSchema.CurrentVersion,
        false,
        false,
        string.Empty,
        SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1,
        null,
        Array.Empty<MapViewportCellView>(),
        Array.Empty<MapViewportRowDeltaView>(),
        Array.Empty<MapViewportRegionDeltaView>());

    public static SimulationMapViewportDeltaData FullSnapshot(
        string payloadHash,
        IReadOnlyList<MapViewportCellView> changedCells,
        IReadOnlyList<MapViewportRowDeltaView> changedRows,
        IReadOnlyList<MapViewportRegionDeltaView> changedRegions)
    {
        return new SimulationMapViewportDeltaData(
            SimulationMapViewportDeltaSchema.CurrentVersion,
            true,
            false,
            payloadHash,
            SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1,
            null,
            changedCells,
            changedRows,
            changedRegions);
    }

    public static SimulationMapViewportDeltaData Delta(
        string payloadHash,
        string basePayloadHash,
        IReadOnlyList<MapViewportCellView> changedCells,
        IReadOnlyList<MapViewportRowDeltaView> changedRows,
        IReadOnlyList<MapViewportRegionDeltaView> changedRegions)
    {
        return new SimulationMapViewportDeltaData(
            SimulationMapViewportDeltaSchema.CurrentVersion,
            true,
            true,
            payloadHash,
            SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1,
            basePayloadHash,
            changedCells,
            changedRows,
            changedRegions);
    }
}

public readonly record struct SimulationMapViewportData(
    bool IsAvailable,
    bool HasWorld,
    int Width,
    int Height,
    int CameraX,
    int CameraY,
    int CurrentZ,
    IReadOnlyList<MapViewportCellView> Cells,
    SimulationMapViewportDeltaData Delta = default,
    RuntimeViewportGeometry Viewport = default)
{
    public static SimulationMapViewportData Unavailable { get; } = new(
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        Array.Empty<MapViewportCellView>(),
        SimulationMapViewportDeltaData.Unavailable,
        RuntimeViewportGeometry.Empty);
}
