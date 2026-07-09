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

    internal IReadOnlyList<KeyValuePair<string, int>> GetRequiredMaterialsSnapshot()
    {
        return OrderMaterials(MaterialsRequired).ToArray();
    }

    internal IReadOnlyList<string> GetRequiredMaterialIdsSnapshot()
    {
        return MaterialsRequired.Keys
            .OrderBy(static materialId => materialId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static materialId => materialId, StringComparer.Ordinal)
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
