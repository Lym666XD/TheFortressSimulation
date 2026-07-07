namespace HumanFortress.Contracts.Runtime.Snapshots;

public enum SimulationPlacementPreviewMode
{
    GroundItems,
    MiningDig,
    MiningRamp,
    MiningChannel,
    MiningStairwell,
    MiningStairwellTop,
    ConstructionWall,
    ConstructionFloor,
    ConstructionRamp,
}

public readonly record struct SimulationPlacementPreviewData(
    int X,
    int Y,
    int Width,
    int Height,
    int EligibleCells,
    int TotalCells,
    IReadOnlyList<PlacementPreviewCellView> Cells)
{
    public static SimulationPlacementPreviewData Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        Array.Empty<PlacementPreviewCellView>());
}

public readonly record struct PlacementPreviewCellView(
    int X,
    int Y);
