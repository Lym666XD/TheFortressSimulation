using HumanFortress.Core.Content;
using Newtonsoft.Json.Linq;

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
    /// Vertical alignment mode for ramps. "df" by default.
    /// </summary>
    public string RampVerticalAlignmentMode { get; set; } = "df";

    /// <summary>
    /// Whether ascending a ramp requires high-side support (solid wall) below the top tile.
    /// </summary>
    public bool RampRequiresHighsideSupport { get; set; } = true;

    /// <summary>
    /// Apply corner-check rule for diagonal moves (2D). If true, both orthogonal adjacent tiles must be walkable.
    /// </summary>
    public bool DiagonalCornerCheck { get; set; } = true;

    /// <summary>
    /// Get default tuning values.
    /// </summary>
    public static NavigationTuning Default => new();

    /// <summary>
    /// Load tuning from content registries (tuning.navigation.json). Falls back to defaults.
    /// </summary>
    public static NavigationTuning LoadFromContent()
    {
        var t = Default;
        var obj = ContentRegistry.Instance.GetTuning<JObject>("tuning.navigation", "$");
        if (obj == null) return t;

        t.AllowDiagonals = obj["allow_diagonals"]?.Value<bool?>() ?? t.AllowDiagonals;
        t.RampVerticalAlignmentMode = obj["ramp_vertical_alignment_mode"]?.Value<string?>() ?? t.RampVerticalAlignmentMode;
        t.RampRequiresHighsideSupport = obj["ramp_requires_highside_support"]?.Value<bool?>() ?? t.RampRequiresHighsideSupport;

        var cost = obj["cost"] as JObject;
        if (cost != null)
        {
            t.BaseCost = (ushort)(cost["base"]?.Value<int?>() ?? t.BaseCost);
            t.OrthogonalCost = (ushort)(cost["orthogonal"]?.Value<int?>() ?? t.OrthogonalCost);
            t.DiagonalCost = (ushort)(cost["diagonal"]?.Value<int?>() ?? t.DiagonalCost);
            t.RampDelta = (ushort)(cost["ramp_delta"]?.Value<int?>() ?? t.RampDelta);
            t.StairDelta = (ushort)(cost["stair_delta"]?.Value<int?>() ?? t.StairDelta);
        }

        var diag = obj["diagonal_rules"] as JObject;
        if (diag != null)
        {
            t.DiagonalCornerCheck = diag["corner_check"]?.Value<bool?>() ?? t.DiagonalCornerCheck;
        }

        var fluids = obj["fluids"] as JObject;
        if (fluids != null)
        {
            t.FluidShallowThreshold = (byte)(fluids["shallow_threshold"]?.Value<int?>() ?? t.FluidShallowThreshold);
            t.FluidDeepThreshold = (byte)(fluids["deep_threshold"]?.Value<int?>() ?? t.FluidDeepThreshold);
            t.FluidWadeCost = (ushort)(fluids["wade_cost"]?.Value<int?>() ?? t.FluidWadeCost);
            t.FluidSwimCost = (ushort)(fluids["swim_cost"]?.Value<int?>() ?? t.FluidSwimCost);
        }

        var traffic = obj["traffic"] as JObject;
        if (traffic != null)
        {
            t.TrafficLow = (short)(traffic["low"]?.Value<int?>() ?? t.TrafficLow);
            t.TrafficNormal = (short)(traffic["normal"]?.Value<int?>() ?? t.TrafficNormal);
            t.TrafficHigh = (short)(traffic["high"]?.Value<int?>() ?? t.TrafficHigh);
            t.TrafficRestricted = (short)(traffic["restricted"]?.Value<int?>() ?? t.TrafficRestricted);
        }

        var doors = obj["doors"] as JObject;
        if (doors != null)
        {
            t.DoorClosedBlocks = doors["closed_blocks"]?.Value<bool?>() ?? t.DoorClosedBlocks;
            t.DoorOpenCost = (ushort)(doors["open_cost"]?.Value<int?>() ?? t.DoorOpenCost);
        }

        var budgets = obj["budgets"] as JObject;
        if (budgets != null)
        {
            t.MaxNodesPerSearch = budgets["max_nodes_per_search"]?.Value<int?>() ?? t.MaxNodesPerSearch;
            t.MaxMsPerTickPathing = budgets["max_ms_per_tick_pathing"]?.Value<int?>() ?? t.MaxMsPerTickPathing;
        }

        return t;
    }
}
