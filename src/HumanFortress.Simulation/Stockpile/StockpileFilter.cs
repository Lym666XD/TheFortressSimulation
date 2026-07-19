using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Filter determining which items a stockpile zone accepts.
/// Data-driven per STOCKPILE_SPEC.md.
/// </summary>
internal sealed class StockpileFilter
{
    /// <summary>
    /// Filter mode (whitelist or blacklist).
    /// </summary>
    internal FilterMode Mode { get; set; } = FilterMode.Whitelist;

    /// <summary>
    /// Item tags to filter by.
    /// </summary>
    internal ImmutableHashSet<string> Tags { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Specific item IDs to filter by.
    /// </summary>
    internal ImmutableHashSet<string> ItemIds { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Material IDs to filter by.
    /// </summary>
    internal ImmutableHashSet<string> Materials { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Check if this filter accepts the given item.
    /// </summary>
    internal bool Accepts(ItemStackRef stack)
    {
        if (stack.Handle == 0)
            return false;

        bool hasCriteria = Tags.Count > 0 || ItemIds.Count > 0 || Materials.Count > 0;
        if (!hasCriteria)
            return true;

        bool matchesTags = Tags.Count > 0 && stack.Tags.Any(Tags.Contains);

        bool matchesIds = ItemIds.Count > 0 &&
            !string.IsNullOrWhiteSpace(stack.DefinitionId) &&
            ItemIds.Contains(stack.DefinitionId);

        bool matchesMaterials = Materials.Count > 0 &&
            !string.IsNullOrWhiteSpace(stack.MaterialId) &&
            Materials.Contains(stack.MaterialId);

        bool matches = matchesTags || matchesIds || matchesMaterials;

        return Mode == FilterMode.Whitelist ? matches : !matches;
    }

    /// <summary>
    /// Create a filter from a preset definition.
    /// </summary>
    internal static StockpileFilter FromPreset(StockpilePreset preset)
    {
        return new StockpileFilter
        {
            Mode = preset.Mode,
            Tags = ToImmutableIdSet(preset.Tags),
            ItemIds = ToImmutableIdSet(preset.ItemIds),
            Materials = ToImmutableIdSet(preset.Materials)
        };
    }

    private static ImmutableHashSet<string> ToImmutableIdSet(IEnumerable<string>? values)
    {
        return values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToImmutableHashSet(StringComparer.Ordinal)
            ?? ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
    }
}

/// <summary>
/// Filter mode for stockpile zones.
/// </summary>
internal enum FilterMode
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
internal sealed class StockpilePreset
{
    internal string Id { get; set; } = "";
    internal string Name { get; set; } = "";
    internal FilterMode Mode { get; set; } = FilterMode.Whitelist;
    internal List<string>? Tags { get; set; }
    internal List<string>? ItemIds { get; set; }
    internal List<string>? Materials { get; set; }
    internal int Priority { get; set; } = 1;
}

/// <summary>
/// Reference to an item stack with stockpile-related properties.
/// </summary>
internal struct ItemStackRef
{
    /// <summary>
    /// Unique handle for this item stack.
    /// </summary>
    internal ulong Handle { get; init; }

    /// <summary>
    /// Static item definition id for filter matching.
    /// </summary>
    internal string DefinitionId { get; init; }

    /// <summary>
    /// Tags projected from the static item definition.
    /// </summary>
    internal ImmutableHashSet<string> Tags { get; init; }

    /// <summary>
    /// Runtime material id or the static fixed material id.
    /// </summary>
    internal string? MaterialId { get; init; }

    /// <summary>
    /// Last zone this item was in (for stickiness).
    /// </summary>
    internal int LastZoneId { get; set; }

    /// <summary>
    /// Tick when this item was placed (for dwell time).
    /// </summary>
    internal ulong PlacedTick { get; set; }

    /// <summary>
    /// Whether this item is reserved by a haul job.
    /// </summary>
    internal bool Reserved { get; set; }

    /// <summary>
    /// Job that has reserved this item.
    /// </summary>
    internal int ReservedByJobId { get; set; }

    internal ItemStackRef(
        ulong handle,
        string definitionId = "",
        IEnumerable<string>? tags = null,
        string? materialId = null,
        int lastZoneId = 0,
        ulong placedTick = 0,
        bool reserved = false,
        int reservedByJobId = 0)
    {
        Handle = handle;
        DefinitionId = definitionId ?? string.Empty;
        Tags = tags?
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .ToImmutableHashSet(StringComparer.Ordinal)
            ?? ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
        MaterialId = materialId;
        LastZoneId = lastZoneId;
        PlacedTick = placedTick;
        Reserved = reserved;
        ReservedByJobId = reservedByJobId;
    }
}
