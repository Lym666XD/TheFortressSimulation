using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Runtime state for a construction site placeable.
/// </summary>
internal sealed class ConstructionSiteState
{
    /// <summary>
    /// Target construction id (e.g., core_construction_workshop_* or l0.* synthetic ids).
    /// </summary>
    internal string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// Required materials by item tag (e.g., stone_block, wood_log, clay_brick).
    /// </summary>
    internal Dictionary<string, int> MaterialsRequired { get; set; } = new();

    /// <summary>
    /// Delivered materials by item tag (cached/derived). Planner may recompute on Read.
    /// </summary>
    internal Dictionary<string, int> MaterialsDelivered { get; set; } = new();

    /// <summary>
    /// Build progress (ticks) and total required.
    /// </summary>
    internal int BuildProgressTicks { get; set; }

    internal int TotalBuildTicks { get; set; }

    internal IReadOnlyList<KeyValuePair<string, int>> GetRequiredMaterialsSnapshot()
    {
        return OrderMaterials(MaterialsRequired).ToArray();
    }

    internal IReadOnlyList<string> GetRequiredMaterialIdsSnapshot()
    {
        return OrderMaterials(MaterialsRequired)
            .Select(static entry => entry.Key)
            .ToArray();
    }

    internal IReadOnlyList<KeyValuePair<string, int>> GetDeliveredMaterialsSnapshot()
    {
        return OrderMaterials(MaterialsDelivered).ToArray();
    }

    private static IOrderedEnumerable<KeyValuePair<string, int>> OrderMaterials(
        IEnumerable<KeyValuePair<string, int>> materials)
    {
        return materials
            .OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.Key, StringComparer.Ordinal);
    }
}
