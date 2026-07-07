using System.Text.Json;

namespace HumanFortress.Contracts.Content.Registry;

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

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
        }
        catch
        {
            return t;
        }

        using (document)
        {
            var obj = document.RootElement;
            if (obj.ValueKind != JsonValueKind.Object) return t;

            t.FloorPlankCount = GetInt(obj, "floor_plank_count") ?? t.FloorPlankCount;
            t.FloorBlockCount = GetInt(obj, "floor_block_count") ?? t.FloorBlockCount;
            t.WallBlockCount = GetInt(obj, "wall_block_count") ?? t.WallBlockCount;
            t.RampBlockCount = GetInt(obj, "ramp_block_count") ?? t.RampBlockCount;
            t.RampPlankCount = GetInt(obj, "ramp_plank_count") ?? t.RampPlankCount;
            t.StairBlockCount = GetInt(obj, "stair_block_count") ?? t.StairBlockCount;
            t.FloorRequiresSupport = GetBool(obj, "floor_requires_support") ?? t.FloorRequiresSupport;
            t.FloorAllowNeighborSupport = GetBool(obj, "floor_allow_neighbor_support") ?? t.FloorAllowNeighborSupport;
            t.BuildRateTicks = GetInt(obj, "build_rate_ticks") ?? t.BuildRateTicks;
            t.BuildTicksWall = GetInt(obj, "build_ticks_wall") ?? t.BuildTicksWall;
            t.BuildTicksFloor = GetInt(obj, "build_ticks_floor") ?? t.BuildTicksFloor;
            t.BuildTicksRamp = GetInt(obj, "build_ticks_ramp") ?? t.BuildTicksRamp;
            t.BuildTicksStairs = GetInt(obj, "build_ticks_stairs") ?? t.BuildTicksStairs;
        }

        return t;
    }

    private static int? GetInt(JsonElement obj, string property)
    {
        return obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
    }

    private static bool? GetBool(JsonElement obj, string property)
    {
        return obj.TryGetProperty(property, out var value)
            && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }
}
