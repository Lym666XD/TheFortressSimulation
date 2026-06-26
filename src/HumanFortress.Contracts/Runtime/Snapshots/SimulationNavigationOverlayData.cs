namespace HumanFortress.Contracts.Runtime.Snapshots;

public enum SimulationNavigationOverlayMode
{
    None,
    Walkability,
    MovementCost,
    Traffic,
    Connectivity,
    PathDisplay,
    FlowField,
    RampMask,
}

public readonly record struct SimulationNavigationOverlayData(
    SimulationNavigationOverlayMode Mode,
    IReadOnlyList<NavigationOverlayCellView> Cells,
    ushort MovementBaseCost)
{
    public static SimulationNavigationOverlayData Empty { get; } = EmptyFor(SimulationNavigationOverlayMode.None, 10);

    public static SimulationNavigationOverlayData EmptyFor(SimulationNavigationOverlayMode mode, ushort movementBaseCost)
    {
        return new SimulationNavigationOverlayData(mode, Array.Empty<NavigationOverlayCellView>(), movementBaseCost);
    }
}

public readonly record struct NavigationOverlayCellView(
    int X,
    int Y,
    char Glyph,
    string ColorHex);

public readonly record struct SimulationNavigationPathData(
    bool HasResult,
    string Kind,
    int Length,
    uint TotalCost,
    IReadOnlyList<NavigationOverlayCellView> Cells)
{
    public static SimulationNavigationPathData Unavailable { get; } = new(
        false,
        "Unavailable",
        0,
        0,
        Array.Empty<NavigationOverlayCellView>());
}
