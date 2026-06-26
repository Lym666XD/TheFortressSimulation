using System.Text.Json;

namespace HumanFortress.Navigation;

/// <summary>
/// Tunable navigation parameters from tuning.navigation.json.
/// Per NAVIGATION_SPEC.md section 10.
/// </summary>
internal sealed class NavigationTuning
{
    /// <summary>
    /// Allow diagonal movement (8-neighbor).
    /// </summary>
    internal bool AllowDiagonals { get; set; } = false;

    /// <summary>
    /// Base movement cost.
    /// </summary>
    internal ushort BaseCost { get; set; } = 10;

    /// <summary>
    /// Orthogonal step weight.
    /// </summary>
    internal ushort OrthogonalCost { get; set; } = 10;

    /// <summary>
    /// Diagonal step weight (approx sqrt(2) * OrthogonalCost).
    /// </summary>
    internal ushort DiagonalCost { get; set; } = 14;

    /// <summary>
    /// Additional cost for ramp movement.
    /// </summary>
    internal ushort RampDelta { get; set; } = 6;

    /// <summary>
    /// Additional cost for stair movement.
    /// </summary>
    internal ushort StairDelta { get; set; } = 8;

    /// <summary>
    /// Fluid depth threshold for shallow (wadeable).
    /// </summary>
    internal byte FluidShallowThreshold { get; set; } = 1;

    /// <summary>
    /// Fluid depth threshold for deep (blocks non-swimmers).
    /// </summary>
    internal byte FluidDeepThreshold { get; set; } = 6;

    /// <summary>
    /// Cost for wading through shallow fluid.
    /// </summary>
    internal ushort FluidWadeCost { get; set; } = 6;

    /// <summary>
    /// Cost for swimming through deep fluid.
    /// </summary>
    internal ushort FluidSwimCost { get; set; } = 18;

    /// <summary>
    /// Traffic adjustment for low-traffic areas.
    /// </summary>
    internal short TrafficLow { get; set; } = -2;

    /// <summary>
    /// Traffic adjustment for normal areas.
    /// </summary>
    internal short TrafficNormal { get; set; } = 0;

    /// <summary>
    /// Traffic adjustment for high-traffic areas.
    /// </summary>
    internal short TrafficHigh { get; set; } = 2;

    /// <summary>
    /// Traffic adjustment for restricted areas.
    /// </summary>
    internal short TrafficRestricted { get; set; } = 8;

    /// <summary>
    /// Extra cost for passing through open doors.
    /// </summary>
    internal ushort DoorOpenCost { get; set; } = 4;

    /// <summary>
    /// Whether closed doors block movement.
    /// </summary>
    internal bool DoorClosedBlocks { get; set; } = true;

    /// <summary>
    /// Maximum nodes to explore per search.
    /// </summary>
    internal int MaxNodesPerSearch { get; set; } = 10000;

    /// <summary>
    /// Maximum milliseconds per tick for pathfinding.
    /// </summary>
    internal int MaxMsPerTickPathing { get; set; } = 3;

    /// <summary>
    /// Vertical alignment mode for ramps. "df" by default.
    /// </summary>
    internal string RampVerticalAlignmentMode { get; set; } = "df";

    /// <summary>
    /// Whether ascending a ramp requires high-side support (solid wall) below the top tile.
    /// </summary>
    internal bool RampRequiresHighsideSupport { get; set; } = true;

    /// <summary>
    /// Apply corner-check rule for diagonal moves (2D). If true, both orthogonal adjacent tiles must be walkable.
    /// </summary>
    internal bool DiagonalCornerCheck { get; set; } = true;

    /// <summary>
    /// Get default tuning values.
    /// </summary>
    internal static NavigationTuning Default => new();

    /// <summary>
    /// Load tuning from serialized tuning.navigation.json content. Falls back to defaults.
    /// </summary>
    internal static NavigationTuning LoadFromJson(string? json)
    {
        var t = Default;
        if (string.IsNullOrWhiteSpace(json))
            return t;

        try
        {
            using var document = JsonDocument.Parse(json);
            var obj = document.RootElement;
            if (obj.ValueKind != JsonValueKind.Object)
                return t;

            t.AllowDiagonals = ReadBool(obj, "allow_diagonals") ?? t.AllowDiagonals;
            t.RampVerticalAlignmentMode = ReadString(obj, "ramp_vertical_alignment_mode") ?? t.RampVerticalAlignmentMode;
            t.RampRequiresHighsideSupport = ReadBool(obj, "ramp_requires_highside_support") ?? t.RampRequiresHighsideSupport;

            if (TryGetObject(obj, "cost", out var cost))
            {
                t.BaseCost = ReadUInt16(cost, "base") ?? t.BaseCost;
                t.OrthogonalCost = ReadUInt16(cost, "orthogonal") ?? t.OrthogonalCost;
                t.DiagonalCost = ReadUInt16(cost, "diagonal") ?? t.DiagonalCost;
                t.RampDelta = ReadUInt16(cost, "ramp_delta") ?? t.RampDelta;
                t.StairDelta = ReadUInt16(cost, "stair_delta") ?? t.StairDelta;
            }

            if (TryGetObject(obj, "diagonal_rules", out var diag))
            {
                t.DiagonalCornerCheck = ReadBool(diag, "corner_check") ?? t.DiagonalCornerCheck;
            }

            if (TryGetObject(obj, "fluids", out var fluids))
            {
                t.FluidShallowThreshold = ReadByte(fluids, "shallow_threshold") ?? t.FluidShallowThreshold;
                t.FluidDeepThreshold = ReadByte(fluids, "deep_threshold") ?? t.FluidDeepThreshold;
                t.FluidWadeCost = ReadUInt16(fluids, "wade_cost") ?? t.FluidWadeCost;
                t.FluidSwimCost = ReadUInt16(fluids, "swim_cost") ?? t.FluidSwimCost;
            }

            if (TryGetObject(obj, "traffic", out var traffic))
            {
                t.TrafficLow = ReadInt16(traffic, "low") ?? t.TrafficLow;
                t.TrafficNormal = ReadInt16(traffic, "normal") ?? t.TrafficNormal;
                t.TrafficHigh = ReadInt16(traffic, "high") ?? t.TrafficHigh;
                t.TrafficRestricted = ReadInt16(traffic, "restricted") ?? t.TrafficRestricted;
            }

            if (TryGetObject(obj, "doors", out var doors))
            {
                t.DoorClosedBlocks = ReadBool(doors, "closed_blocks") ?? t.DoorClosedBlocks;
                t.DoorOpenCost = ReadUInt16(doors, "open_cost") ?? t.DoorOpenCost;
            }

            if (TryGetObject(obj, "budgets", out var budgets))
            {
                t.MaxNodesPerSearch = ReadInt32(budgets, "max_nodes_per_search") ?? t.MaxNodesPerSearch;
                t.MaxMsPerTickPathing = ReadInt32(budgets, "max_ms_per_tick_pathing") ?? t.MaxMsPerTickPathing;
            }
        }
        catch (JsonException)
        {
            return Default;
        }

        return t;
    }

    private static bool TryGetObject(JsonElement parent, string propertyName, out JsonElement value)
    {
        return parent.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.Object;
    }

    private static bool? ReadBool(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static string? ReadString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        return value.GetString();
    }

    private static int? ReadInt32(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || !value.TryGetInt32(out var number))
            return null;

        return number;
    }

    private static ushort? ReadUInt16(JsonElement parent, string propertyName)
    {
        var value = ReadInt32(parent, propertyName);
        if (!value.HasValue || value.Value < ushort.MinValue || value.Value > ushort.MaxValue)
            return null;

        return (ushort)value.Value;
    }

    private static short? ReadInt16(JsonElement parent, string propertyName)
    {
        var value = ReadInt32(parent, propertyName);
        if (!value.HasValue || value.Value < short.MinValue || value.Value > short.MaxValue)
            return null;

        return (short)value.Value;
    }

    private static byte? ReadByte(JsonElement parent, string propertyName)
    {
        var value = ReadInt32(parent, propertyName);
        if (!value.HasValue || value.Value < byte.MinValue || value.Value > byte.MaxValue)
            return null;

        return (byte)value.Value;
    }
}
