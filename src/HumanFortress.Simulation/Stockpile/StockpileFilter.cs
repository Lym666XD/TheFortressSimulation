using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using HumanFortress.Core.Content;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Filter determining which items a stockpile zone accepts.
/// Data-driven per STOCKPILE_SPEC.md.
/// </summary>
public sealed class StockpileFilter
{
    /// <summary>
    /// Filter mode (whitelist or blacklist).
    /// </summary>
    public FilterMode Mode { get; set; } = FilterMode.Whitelist;

    /// <summary>
    /// Item tags to filter by.
    /// </summary>
    public ImmutableHashSet<string> Tags { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Specific item IDs to filter by.
    /// </summary>
    public ImmutableHashSet<string> ItemIds { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Material IDs to filter by.
    /// </summary>
    public ImmutableHashSet<string> Materials { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Check if this filter accepts the given item.
    /// </summary>
    public bool Accepts(ItemStackRef stack, ContentRegistry registry)
    {
        if (stack.Handle == 0)
            return false;

        // TODO: Get item definition from registry
        // For now, simplified logic
        var itemDef = GetItemDefinition(stack, registry);
        if (itemDef == null)
            return false;

        bool matchesTags = Tags.Count == 0 ||
            (itemDef.Tags != null && itemDef.Tags.Any(t => Tags.Contains(t)));

        bool matchesIds = ItemIds.Count == 0 ||
            ItemIds.Contains(itemDef.Id);

        bool matchesMaterials = Materials.Count == 0 ||
            (itemDef.FixedMaterial != null && Materials.Contains(itemDef.FixedMaterial));

        bool matches = matchesTags || matchesIds || matchesMaterials;

        return Mode == FilterMode.Whitelist ? matches : !matches;
    }

    /// <summary>
    /// Create a filter from a preset definition.
    /// </summary>
    public static StockpileFilter FromPreset(StockpilePreset preset)
    {
        return new StockpileFilter
        {
            Mode = preset.Mode,
            Tags = preset.Tags?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
            ItemIds = preset.ItemIds?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
            Materials = preset.Materials?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty
        };
    }

    private ItemDefinition? GetItemDefinition(ItemStackRef stack, ContentRegistry registry)
    {
        // TODO: Implement actual item lookup from registry
        // This requires integration with the item system
        return null;
    }
}

/// <summary>
/// Filter mode for stockpile zones.
/// </summary>
public enum FilterMode
{
    /// <summary>
    /// Only accept items matching the filter criteria.
    /// </summary>
    Whitelist,

    /// <summary>
    /// Accept all items except those matching the filter criteria.
    /// </summary>
    Blacklist
}

/// <summary>
/// Stockpile preset loaded from JSON.
/// </summary>
public sealed class StockpilePreset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public FilterMode Mode { get; set; } = FilterMode.Whitelist;
    public List<string>? Tags { get; set; }
    public List<string>? ItemIds { get; set; }
    public List<string>? Materials { get; set; }
    public int Priority { get; set; } = 1;
}

/// <summary>
/// Temporary item definition placeholder.
/// TODO: Replace with actual item system integration.
/// </summary>
internal class ItemDefinition
{
    public string Id { get; set; } = "";
    public List<string>? Tags { get; set; }
    public string? FixedMaterial { get; set; }
}

/// <summary>
/// Reference to an item stack with stockpile-related properties.
/// </summary>
public struct ItemStackRef
{
    /// <summary>
    /// Unique handle for this item stack.
    /// </summary>
    public int Handle { get; init; }

    /// <summary>
    /// Last zone this item was in (for stickiness).
    /// </summary>
    public int LastZoneId { get; set; }

    /// <summary>
    /// Tick when this item was placed (for dwell time).
    /// </summary>
    public ulong PlacedTick { get; set; }

    /// <summary>
    /// Whether this item is reserved by a haul job.
    /// </summary>
    public bool Reserved { get; set; }

    /// <summary>
    /// Job that has reserved this item.
    /// </summary>
    public int ReservedByJobId { get; set; }

    public ItemStackRef(int handle)
    {
        Handle = handle;
        LastZoneId = 0;
        PlacedTick = 0;
        Reserved = false;
        ReservedByJobId = 0;
    }
}