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
    public int StairBlockCount { get; set; } = 2;

    // Support rules for floors
    public bool FloorRequiresSupport { get; set; } = true;
    public bool FloorAllowNeighborSupport { get; set; } = false;

    public static ConstructionTuning Default => new();

    public static ConstructionTuning LoadFromContent()
    {
        var t = Default;
        var obj = HumanFortress.Core.Content.ContentRegistry.Instance.GetTuning<JObject>("tuning.construction", "$");
        if (obj == null) return t;

        t.FloorPlankCount = obj["floor_plank_count"]?.Value<int?>() ?? t.FloorPlankCount;
        t.FloorBlockCount = obj["floor_block_count"]?.Value<int?>() ?? t.FloorBlockCount;
        t.WallBlockCount = obj["wall_block_count"]?.Value<int?>() ?? t.WallBlockCount;
        t.RampBlockCount = obj["ramp_block_count"]?.Value<int?>() ?? t.RampBlockCount;
        t.StairBlockCount = obj["stair_block_count"]?.Value<int?>() ?? t.StairBlockCount;
        t.FloorRequiresSupport = obj["floor_requires_support"]?.Value<bool?>() ?? t.FloorRequiresSupport;
        t.FloorAllowNeighborSupport = obj["floor_allow_neighbor_support"]?.Value<bool?>() ?? t.FloorAllowNeighborSupport;
        return t;
    }
}
