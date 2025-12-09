using System;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Construction definition for on-site built structures (walls, floors, workshops, etc.)
/// Quality is always 0 for constructions (per PLACEABLE_SPEC §15.2)
/// </summary>
public sealed class ConstructionDefinition
{
    // === IDENTITY ===
    /// <summary>
    /// Unique construction definition ID (e.g., "core_construction_wall_stone")
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Display name (material-agnostic, e.g., "Stone Wall")
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Primary category for UI grouping (walls, floors, stairs, doors, workshops, defenses)
    /// </summary>
    public string Category { get; set; } = "";

    // === BUILD REQUIREMENTS ===
    /// <summary>
    /// Base construction time in ticks (modified by worker skill)
    /// </summary>
    public int BuildTimeTicks { get; set; }

    /// <summary>
    /// Required materials for construction
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays (JSON deserialization)
    public MaterialCost[] MaterialCosts { get; set; } = Array.Empty<MaterialCost>();
#pragma warning restore CA1819

    /// <summary>
    /// Optional skill tag required for construction (e.g., "skill_masonry", "skill_carpentry")
    /// </summary>
    public string? SkillRequired { get; set; }

    // === PLACEABLE PROPERTIES ===
    /// <summary>
    /// Placeable properties (footprint, effects, passability)
    /// Effects are fixed (no quality modifier for constructions)
    /// </summary>
    public PlaceableProfile PlaceableProfile { get; set; } = new();

    // === WORKSHOP PROPERTIES (optional, only for workshop constructions) ===
    /// <summary>
    /// Workshop IO slots configuration (optional, only for workshops)
    /// </summary>
    public WorkshopIo? Io { get; set; }

    /// <summary>
    /// Available attachment slot types (optional, only for workshops)
    /// </summary>
#pragma warning disable CA1819
    public string[]? AttachmentSlots { get; set; }
#pragma warning restore CA1819

    /// <summary>
    /// Baseline power consumption in watts (optional, only for workshops)
    /// </summary>
    public int? PowerBaselineW { get; set; }

    /// <summary>
    /// Minimum era for this workshop (optional, C/M/R)
    /// </summary>
    public string? EraMin { get; set; }

    /// <summary>
    /// Maximum era for this workshop (optional, C/M/R)
    /// </summary>
    public string? EraMax { get; set; }

    /// <summary>
    /// Workshop attachments definitions (optional, only for workshops)
    /// </summary>
#pragma warning disable CA1819
    public WorkshopAttachment[]? Attachments { get; set; }
#pragma warning restore CA1819

    public void Validate()
    {
        if (string.IsNullOrEmpty(Id))
            throw new InvalidOperationException("Construction ID cannot be empty");
        if (!Id.StartsWith("core_construction_", StringComparison.Ordinal))
            throw new InvalidOperationException($"Construction ID must start with 'core_construction_': {Id}");
        if (string.IsNullOrEmpty(Name))
            throw new InvalidOperationException($"Construction '{Id}' has no name");
        if (BuildTimeTicks < 1)
            throw new InvalidOperationException($"Construction '{Id}' has invalid build time: {BuildTimeTicks}");
        if (MaterialCosts.Length == 0)
            throw new InvalidOperationException($"Construction '{Id}' has no material costs");
        if (PlaceableProfile == null)
            throw new InvalidOperationException($"Construction '{Id}' has no placeable profile");
    }
}

/// <summary>
/// Material cost entry for constructions
/// Can specify either a tag (e.g., "block", "bar") or a specific item definition ID
/// </summary>
public sealed class MaterialCost
{
    /// <summary>
    /// Material tag filter (e.g., "block", "bar", "log") - mutually exclusive with DefId
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Specific item definition ID (e.g., "core_item_component_mechanism") - mutually exclusive with Tag
    /// </summary>
    public string? DefId { get; set; }

    /// <summary>
    /// Number of items required
    /// </summary>
    public int Count { get; set; }

    public void Validate()
    {
        if (string.IsNullOrEmpty(Tag) && string.IsNullOrEmpty(DefId))
            throw new InvalidOperationException("MaterialCost must specify either Tag or DefId");
        if (!string.IsNullOrEmpty(Tag) && !string.IsNullOrEmpty(DefId))
            throw new InvalidOperationException($"MaterialCost cannot specify both Tag and DefId: tag={Tag}, defId={DefId}");
        if (Count < 1)
            throw new InvalidOperationException($"MaterialCost count must be >= 1: {Count}");
    }

    public override string ToString()
    {
        var identifier = !string.IsNullOrEmpty(Tag) ? $"tag:{Tag}" : $"def:{DefId}";
        return $"{Count}× {identifier}";
    }
}

/// <summary>
/// Workshop IO slots configuration
/// </summary>
public sealed class WorkshopIo
{
    /// <summary>
    /// Number of input slots for this workshop
    /// </summary>
    public int InputSlots { get; set; }

    /// <summary>
    /// Number of output slots for this workshop
    /// </summary>
    public int OutputSlots { get; set; }

    /// <summary>
    /// Number of buffer slots for this workshop
    /// </summary>
    public int BufferSlots { get; set; }
}

/// <summary>
/// Workshop attachment definition (e.g., kiln, water saw, etc.)
/// </summary>
public sealed class WorkshopAttachment
{
    /// <summary>
    /// Unique attachment ID (e.g., "core_attach_stone_cut_watersaw")
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Display name for this attachment
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Slot type this attachment fits into (e.g., "cutting", "forming")
    /// </summary>
    public string Slot { get; set; } = "";

    /// <summary>
    /// Era when this attachment becomes available (C/M/R)
    /// </summary>
    public string? Era { get; set; }

    /// <summary>
    /// Minimum era for this attachment (optional)
    /// </summary>
    public string? EraMin { get; set; }

    /// <summary>
    /// Maximum era for this attachment (optional)
    /// </summary>
    public string? EraMax { get; set; }

    /// <summary>
    /// ID of the attachment this upgrades to (null if final tier)
    /// </summary>
    public string? UpgradeTo { get; set; }

    /// <summary>
    /// Tags for this attachment (e.g., "waterpower", "calcination")
    /// </summary>
#pragma warning disable CA1819
    public string[]? Tags { get; set; }
#pragma warning restore CA1819

    /// <summary>
    /// Power consumption in watts for this attachment
    /// </summary>
    public int PowerW { get; set; }
}
