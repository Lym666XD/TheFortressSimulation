using Newtonsoft.Json.Linq;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Tuning for L0 construction material costs per tile. Values are counts of items required.
/// </summary>
public sealed class ConstructionTuning
{
    // Defaults (can be overridden by tuning.construction.json)
    public int FloorPlankCount { get; set; } = 1;
    public int FloorBlockCount { get; set; } = 1;
    public int WallBlockCount { get; set; } = 1;
    public int RampBlockCount { get; set; } = 1;
    public int RampPlankCount { get; set; } = 1;
    public int StairBlockCount { get; set; } = 2;
    // Build timings (ticks)
    public int BuildRateTicks { get; set; } = 50; // progress per executor write
    public int BuildTicksWall { get; set; } = 600;
    public int BuildTicksFloor { get; set; } = 300;
    public int BuildTicksRamp { get; set; } = 500;
    public int BuildTicksStairs { get; set; } = 800;

    // Support rules for floors
    public bool FloorRequiresSupport { get; set; } = true;
    public bool FloorAllowNeighborSupport { get; set; } = false;

    public static ConstructionTuning Default => new();

    public static ConstructionTuning LoadFromJson(string? json)
    {
        var t = Default;
        if (string.IsNullOrWhiteSpace(json)) return t;

        JObject obj;
        try
        {
            obj = JObject.Parse(json);
        }
        catch
        {
            return t;
        }

        if (obj == null) return t;

        t.FloorPlankCount = obj["floor_plank_count"]?.Value<int?>() ?? t.FloorPlankCount;
        t.FloorBlockCount = obj["floor_block_count"]?.Value<int?>() ?? t.FloorBlockCount;
        t.WallBlockCount = obj["wall_block_count"]?.Value<int?>() ?? t.WallBlockCount;
        t.RampBlockCount = obj["ramp_block_count"]?.Value<int?>() ?? t.RampBlockCount;
        t.RampPlankCount = obj["ramp_plank_count"]?.Value<int?>() ?? t.RampPlankCount;
        t.StairBlockCount = obj["stair_block_count"]?.Value<int?>() ?? t.StairBlockCount;
        t.FloorRequiresSupport = obj["floor_requires_support"]?.Value<bool?>() ?? t.FloorRequiresSupport;
        t.FloorAllowNeighborSupport = obj["floor_allow_neighbor_support"]?.Value<bool?>() ?? t.FloorAllowNeighborSupport;
        t.BuildRateTicks = obj["build_rate_ticks"]?.Value<int?>() ?? t.BuildRateTicks;
        t.BuildTicksWall = obj["build_ticks_wall"]?.Value<int?>() ?? t.BuildTicksWall;
        t.BuildTicksFloor = obj["build_ticks_floor"]?.Value<int?>() ?? t.BuildTicksFloor;
        t.BuildTicksRamp = obj["build_ticks_ramp"]?.Value<int?>() ?? t.BuildTicksRamp;
        t.BuildTicksStairs = obj["build_ticks_stairs"]?.Value<int?>() ?? t.BuildTicksStairs;
        return t;
    }
}
