using System;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Construction definition for on-site built structures (walls, floors, workshops, etc.).
/// Quality is always 0 for constructions.
/// </summary>
public sealed class ConstructionDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int BuildTimeTicks { get; set; }

#pragma warning disable CA1819 // JSON DTO compatibility
    public MaterialCost[] MaterialCosts { get; set; } = Array.Empty<MaterialCost>();
#pragma warning restore CA1819

    public string? SkillRequired { get; set; }
    public PlaceableProfile PlaceableProfile { get; set; } = new();
    public WorkshopIo? Io { get; set; }

#pragma warning disable CA1819 // JSON DTO compatibility
    public string[]? AttachmentSlots { get; set; }
#pragma warning restore CA1819

    public int? PowerBaselineW { get; set; }
    public string? EraMin { get; set; }
    public string? EraMax { get; set; }

#pragma warning disable CA1819 // JSON DTO compatibility
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

public sealed class MaterialCost
{
    public string? Tag { get; set; }
    public string? DefId { get; set; }
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
        return $"{Count}x {identifier}";
    }
}

public sealed class WorkshopIo
{
    public int InputSlots { get; set; }
    public int OutputSlots { get; set; }
    public int BufferSlots { get; set; }
}

public sealed class WorkshopAttachment
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty;
    public string? Era { get; set; }
    public string? EraMin { get; set; }
    public string? EraMax { get; set; }
    public string? UpgradeTo { get; set; }

#pragma warning disable CA1819 // JSON DTO compatibility
    public string[]? Tags { get; set; }
#pragma warning restore CA1819

    public int PowerW { get; set; }
}
