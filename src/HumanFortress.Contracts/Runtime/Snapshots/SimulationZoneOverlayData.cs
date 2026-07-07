namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct SimulationZoneOverlayData(
    IReadOnlyList<ZoneOverlayCellView> Cells)
{
    public static SimulationZoneOverlayData Empty { get; } = new(Array.Empty<ZoneOverlayCellView>());
}

public readonly record struct ZoneOverlayCellView(
    int X,
    int Y,
    char Glyph,
    string ColorHex);

public readonly record struct SimulationZoneDetailData(
    bool HasZone,
    int ZoneId,
    string Name,
    string DisplayName,
    string Category,
    int TotalCells,
    int MemberChunkCount,
    bool Enabled)
{
    public static SimulationZoneDetailData Empty { get; } = new(
        false,
        0,
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        0,
        false);
}

public readonly record struct ZoneHitData(
    bool HasZone,
    int ZoneId,
    int X,
    int Y,
    int Z)
{
    public static ZoneHitData Empty { get; } = new(false, 0, 0, 0, 0);
}
