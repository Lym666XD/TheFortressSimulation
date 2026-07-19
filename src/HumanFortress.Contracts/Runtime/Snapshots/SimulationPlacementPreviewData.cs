using HumanFortress.Contracts.Runtime;

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

public readonly record struct SimulationPlacementPreviewRequestData(
    RuntimePoint First,
    RuntimePoint Second,
    int Z,
    SimulationPlacementPreviewMode Mode)
{
    public SimulationPlacementPreviewRequestData Canonicalize()
    {
        return this with
        {
            First = new RuntimePoint(
                Math.Min(First.X, Second.X),
                Math.Min(First.Y, Second.Y)),
            Second = new RuntimePoint(
                Math.Max(First.X, Second.X),
                Math.Max(First.Y, Second.Y)),
        };
    }

    public static SimulationPlacementPreviewRequestData[] CanonicalizeAll(
        IEnumerable<SimulationPlacementPreviewRequestData>? requests)
    {
        return (requests ?? Array.Empty<SimulationPlacementPreviewRequestData>())
            .Select(static request => request.Canonicalize())
            .Distinct()
            .OrderBy(static request => request.Z)
            .ThenBy(static request => (int)request.Mode)
            .ThenBy(static request => request.First.X)
            .ThenBy(static request => request.First.Y)
            .ThenBy(static request => request.Second.X)
            .ThenBy(static request => request.Second.Y)
            .ToArray();
    }
}

public readonly record struct SimulationPlacementPreviewRowData(
    SimulationPlacementPreviewRequestData Request,
    SimulationPlacementPreviewData Preview);

public readonly record struct SimulationPlacementPreviewFrameData(
    SimulationSnapshotMetadata Metadata,
    IReadOnlyList<SimulationPlacementPreviewRowData> Rows)
{
    public static SimulationPlacementPreviewFrameData Empty { get; } = new(
        default,
        Array.Empty<SimulationPlacementPreviewRowData>());

    public SimulationPlacementPreviewData Find(
        SimulationPlacementPreviewRequestData request)
    {
        var canonical = request.Canonicalize();
        foreach (var row in Rows ?? Array.Empty<SimulationPlacementPreviewRowData>())
        {
            if (row.Request == canonical)
                return row.Preview;
        }

        return SimulationPlacementPreviewData.Empty;
    }
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
