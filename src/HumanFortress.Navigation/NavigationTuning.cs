namespace HumanFortress.Navigation;

/// <summary>
/// Tunable navigation parameters from tuning.navigation.json.
/// Per NAVIGATION_SPEC.md section 10.
/// </summary>
public sealed class NavigationTuning
{
    /// <summary>
    /// Allow diagonal movement (8-neighbor).
    /// </summary>
    public bool AllowDiagonals { get; set; } = false;

    /// <summary>
    /// Base movement cost.
    /// </summary>
    public ushort BaseCost { get; set; } = 10;

    /// <summary>
    /// Orthogonal step weight.
    /// </summary>
    public ushort OrthogonalCost { get; set; } = 10;

    /// <summary>
    /// Diagonal step weight (approx sqrt(2) * OrthogonalCost).
    /// </summary>
    public ushort DiagonalCost { get; set; } = 14;

    /// <summary>
    /// Additional cost for ramp movement.
    /// </summary>
    public ushort RampDelta { get; set; } = 6;

    /// <summary>
    /// Additional cost for stair movement.
    /// </summary>
    public ushort StairDelta { get; set; } = 8;

    /// <summary>
    /// Fluid depth threshold for shallow (wadeable).
    /// </summary>
    public byte FluidShallowThreshold { get; set; } = 1;

    /// <summary>
    /// Fluid depth threshold for deep (blocks non-swimmers).
    /// </summary>
    public byte FluidDeepThreshold { get; set; } = 6;

    /// <summary>
    /// Cost for wading through shallow fluid.
    /// </summary>
    public ushort FluidWadeCost { get; set; } = 6;

    /// <summary>
    /// Cost for swimming through deep fluid.
    /// </summary>
    public ushort FluidSwimCost { get; set; } = 18;

    /// <summary>
    /// Traffic adjustment for low-traffic areas.
    /// </summary>
    public short TrafficLow { get; set; } = -2;

    /// <summary>
    /// Traffic adjustment for normal areas.
    /// </summary>
    public short TrafficNormal { get; set; } = 0;

    /// <summary>
    /// Traffic adjustment for high-traffic areas.
    /// </summary>
    public short TrafficHigh { get; set; } = 2;

    /// <summary>
    /// Traffic adjustment for restricted areas.
    /// </summary>
    public short TrafficRestricted { get; set; } = 8;

    /// <summary>
    /// Extra cost for passing through open doors.
    /// </summary>
    public ushort DoorOpenCost { get; set; } = 4;

    /// <summary>
    /// Whether closed doors block movement.
    /// </summary>
    public bool DoorClosedBlocks { get; set; } = true;

    /// <summary>
    /// Maximum nodes to explore per search.
    /// </summary>
    public int MaxNodesPerSearch { get; set; } = 10000;

    /// <summary>
    /// Maximum milliseconds per tick for pathfinding.
    /// </summary>
    public int MaxMsPerTickPathing { get; set; } = 3;

    /// <summary>
    /// Get default tuning values.
    /// </summary>
    public static NavigationTuning Default => new();
}