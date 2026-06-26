namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct MapViewportCellView(
    int ScreenX,
    int ScreenY,
    int Glyph,
    SnapshotColor Color);

public readonly record struct SimulationMapViewportData(
    bool IsAvailable,
    bool HasWorld,
    int Width,
    int Height,
    int CameraX,
    int CameraY,
    int CurrentZ,
    IReadOnlyList<MapViewportCellView> Cells)
{
    public static SimulationMapViewportData Unavailable { get; } = new(
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        Array.Empty<MapViewportCellView>());
}
