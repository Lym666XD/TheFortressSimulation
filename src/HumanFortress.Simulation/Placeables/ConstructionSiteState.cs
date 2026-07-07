using System.Collections.Generic;

namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Runtime state for a construction site placeable.
/// </summary>
internal sealed class ConstructionSiteState
{
    /// <summary>
    /// Target construction id (e.g., core_construction_workshop_* or l0.* synthetic ids).
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// Required materials by item tag (e.g., stone_block, wood_log, clay_brick).
    /// </summary>
    public Dictionary<string, int> MaterialsRequired { get; set; } = new();

    /// <summary>
    /// Delivered materials by item tag (cached/derived). Planner may recompute on Read.
    /// </summary>
    public Dictionary<string, int> MaterialsDelivered { get; set; } = new();

    /// <summary>
    /// Build progress (ticks) and total required.
    /// </summary>
    public int BuildProgressTicks { get; set; }

    public int TotalBuildTicks { get; set; }
}
