namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct SimulationStockpileOverlayData(
    IReadOnlyList<StockpileOverlayCellView> Cells)
{
    public static SimulationStockpileOverlayData Empty { get; } = new(Array.Empty<StockpileOverlayCellView>());
}

public readonly record struct StockpileOverlayCellView(
    int X,
    int Y);

public readonly record struct SimulationStockpileDetailData(
    bool HasZone,
    int ZoneId,
    string Name,
    int Priority,
    string PriorityName,
    string FilterSummary,
    int UsedCells,
    int TotalCells)
{
    public static SimulationStockpileDetailData Empty { get; } = new(
        false,
        0,
        string.Empty,
        0,
        string.Empty,
        string.Empty,
        0,
        0);
}

public readonly record struct StockpileHitData(
    bool HasZone,
    int ZoneId,
    SnapshotPoint WorldPosition)
{
    public static StockpileHitData Empty { get; } = new(false, 0, new SnapshotPoint(0, 0));
}
