using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Random;
using HumanFortress.Simulation.Items;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Runtime placeable instance stored in chunk.
/// Can be created from:
/// - Installable: ItemInstance with PlaceableProfile (preserves quality, material, decorations)
/// - Construction: Built on-site from ConstructionDefinition (quality always 0)
///
/// NOTE: PlaceableData is serialized to chunk saves when save system is implemented.
/// Currently stored in memory only via Chunk.PlaceableData layer.
/// </summary>
public sealed class PlaceableInstance
{
    private const ulong UninstalledItemGuidScope = 0x554E494E53544954UL;

    // === IDENTITY ===
    /// <summary>
    /// Unique GUID for this placeable instance
    /// </summary>
    public Guid Guid { get; }

    /// <summary>
    /// Placeable kind (Installable from item, or Construction built on-site)
    /// </summary>
    public PlaceableKind Kind { get; }

    /// <summary>
    /// Definition ID (item def ID for Installable, construction def ID for Construction)
    /// </summary>
    public string DefinitionId { get; }

    // === LOCATION ===
    /// <summary>
    /// World position (anchor point, top-left for MVP)
    /// </summary>
    public Point Position { get; }

    /// <summary>
    /// Z-level
    /// </summary>
    public int Z { get; }

    /// <summary>
    /// Footprint dimensions (stored directly, no rotation in MVP)
    /// </summary>
    public Footprint Footprint { get; }

    // === SOURCE TRACKING (Installable only) ===
    /// <summary>
    /// Source item GUID (reference only, for uninstall tracking)
    /// Only set for Installable kind
    /// </summary>
    public Guid? SourceItemGuid { get; set; }

    /// <summary>
    /// Source item material ID (preserved for uninstall)
    /// Only set for Installable kind
    /// </summary>
    public string? SourceItemMaterial { get; set; }

    /// <summary>
    /// Source item quality tier (preserved for uninstall)
    /// Only set for Installable kind (-3 to +3)
    /// </summary>
    public int SourceItemQuality { get; set; }

    /// <summary>
    /// Source item decorations (inlays, engravings, sockets - preserved for uninstall)
    /// Only set for Installable kind
    /// </summary>
    public List<Improvement>? SourceItemDecorations { get; set; }

    /// <summary>
    /// Source item maker (crafter GUID - preserved for uninstall)
    /// Only set for Installable kind
    /// </summary>
    public Guid? SourceItemMaker { get; set; }

    // === EFFECTS (computed values, strategy B) ===
    /// <summary>
    /// Environmental effects (computed at install/build time)
    /// For Installable: base effects + quality modifier
    /// For Construction: fixed effects (quality always 0)
    /// </summary>
    public EffectsBlock Effects { get; set; } = new();

    /// <summary>
    /// Passability mode (Blocking/Nonblocking/Doorway). Defaults to Nonblocking for ghosts.
    /// </summary>
    public PassabilityMode Passability { get; set; } = PassabilityMode.Nonblocking;

    /// <summary>
    /// True if this is a temporary construction ghost placeholder.
    /// </summary>
    public bool IsGhost { get; set; } = false;

    /// <summary>
    /// Optional state when this instance represents a construction site.
    /// Tracks target, required materials, delivered materials (derived or cached), and build progress.
    /// </summary>
    public ConstructionSiteState? ConstructionSite { get; set; }

    /// <summary>
    /// Optional workshop state (set for completed workshop constructions).
    /// </summary>
    public WorkshopState? Workshop { get; set; }

    // === STATE MACHINES ===
    /// <summary>
    /// Door state (only if passability=doorway)
    /// </summary>
    public DoorState? DoorState { get; set; }

    // === OWNERSHIP ===
    /// <summary>
    /// Owner faction ID
    /// </summary>
    public string? OwnerFactionId { get; set; }

    /// <summary>
    /// Owner creature GUID
    /// </summary>
    public Guid? OwnerCreatureGuid { get; set; }

    /// <summary>
    /// Forbidden flag (blocks usage)
    /// </summary>
    public bool Forbidden { get; set; }

    // === CONDITION ===
    /// <summary>
    /// Current hit points
    /// </summary>
    public int HitPoints { get; set; }

    /// <summary>
    /// Maximum hit points (calculated from material and size)
    /// </summary>
    public int MaxHitPoints { get; set; }

    public PlaceableInstance(
        Guid guid,
        PlaceableKind kind,
        string definitionId,
        Point position,
        int z,
        Footprint footprint)
    {
        Guid = guid;
        Kind = kind;
        DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
        Position = position;
        Z = z;
        Footprint = footprint;
    }

    /// <summary>
    /// Create placeable instance from installed item
    /// </summary>
    public static PlaceableInstance CreateFromItem(
        ItemInstance sourceItem,
        ItemDefinition def,
        Point position,
        int z,
        ulong tickSeed,
        PlaceableTuning? tuning = null)
    {
        if (def.PlaceableProfile == null)
            throw new InvalidOperationException($"Item {def.Id} has no placeable_profile");

        // Load tuning if not provided
        tuning ??= PlaceableTuning.Default;

        // Generate deterministic GUID from position and tick seed
        var guid = DeterministicGuidGenerator.GenerateFromPosition(tickSeed, position.X, position.Y, z);

        var instance = new PlaceableInstance(
            guid: guid,
            kind: PlaceableKind.Installable,
            definitionId: def.Id,
            position: position,
            z: z,
            footprint: def.PlaceableProfile.Footprint);

        // Snapshot source item properties
        instance.SourceItemGuid = sourceItem.Guid;
        instance.SourceItemMaterial = sourceItem.MaterialId;
        instance.SourceItemQuality = sourceItem.QualityTier;
        instance.SourceItemDecorations = sourceItem.Improvements != null
            ? new List<Improvement>(sourceItem.Improvements)
            : null;
        instance.SourceItemMaker = sourceItem.CraftedBy;

        // Compute effects with quality modifier from tuning
        var baseEffects = def.PlaceableProfile.Effects;
        instance.Effects = new EffectsBlock
        {
            Beauty = baseEffects.Beauty + (sourceItem.QualityTier * tuning.BeautyPerTier),
            Comfort = baseEffects.Comfort + (sourceItem.QualityTier * tuning.ComfortPerTier),
            LightLumen = baseEffects.LightLumen,
            HeatW = baseEffects.HeatW
        };

        // Calculate HP from material and volume (using tuning parameters)
        float hpMultiplier = tuning.MaterialHPMultiplier.GetValueOrDefault("default", 1.0f);
        if (!string.IsNullOrEmpty(sourceItem.MaterialId))
        {
            // Extract material category from ID (e.g., "core_mat_metal_iron" -> "metal")
            var parts = sourceItem.MaterialId.Split('_');
            if (parts.Length >= 3)
            {
                string matCategory = parts[2]; // "metal", "stone", "wood", etc.
                hpMultiplier = tuning.MaterialHPMultiplier.GetValueOrDefault(matCategory, hpMultiplier);
            }
        }

        int baseHP = (int)(def.BaseVolumeML * tuning.HPPerVolumeML * hpMultiplier);
        instance.MaxHitPoints = baseHP > 0 ? baseHP : tuning.DefaultMaxHP;
        instance.HitPoints = instance.MaxHitPoints;

        return instance;
    }

    /// <summary>
    /// Create placeable instance from construction
    /// </summary>
    public static PlaceableInstance CreateFromConstruction(
        ConstructionDefinition def,
        Point position,
        int z,
        ulong tickSeed,
        PlaceableTuning? tuning = null)
    {
        if (def.PlaceableProfile == null)
            throw new InvalidOperationException($"Construction {def.Id} has no placeable_profile");

        // Load tuning if not provided
        tuning ??= PlaceableTuning.Default;

        // Generate deterministic GUID from position and tick seed
        var guid = DeterministicGuidGenerator.GenerateFromPosition(tickSeed, position.X, position.Y, z);

        var instance = new PlaceableInstance(
            guid: guid,
            kind: PlaceableKind.Construction,
            definitionId: def.Id,
            position: position,
            z: z,
            footprint: def.PlaceableProfile.Footprint);

        // Copy fixed effects (no quality modifier for constructions)
        instance.Effects = def.PlaceableProfile.Effects.Clone();
        // Apply passability from definition (data-driven)
        instance.Passability = def.PlaceableProfile.Passability;

        // Calculate HP from material costs (simplified: sum of all material counts * default HP)
        int totalMaterialCount = 0;
        foreach (var cost in def.MaterialCosts)
        {
            totalMaterialCount += cost.Count;
        }

        instance.MaxHitPoints = totalMaterialCount > 0
            ? totalMaterialCount * tuning.DefaultMaxHP
            : tuning.DefaultMaxHP;
        instance.HitPoints = instance.MaxHitPoints;

        if (IsWorkshopDefinition(def))
        {
            instance.Workshop ??= new WorkshopState();
            int maxWorkers = Math.Max(1, def.Io?.InputSlots ?? 1);
            instance.Workshop.ConfigureWorkers(1, maxWorkers);
        }

        return instance;
    }

    /// <summary>
    /// Create item instance from uninstalled placeable (generates deterministic new GUID per SPEC §15.6)
    /// </summary>
    public static ItemInstance CreateItemFromPlaceable(
        PlaceableInstance placeable,
        ItemDefinition def,
        ulong currentTick,
        PlaceableTuning? tuning = null)
    {
        if (placeable.Kind != PlaceableKind.Installable)
            throw new InvalidOperationException($"Cannot uninstall Construction placeable {placeable.DefinitionId}");

        // Load tuning if not provided
        tuning ??= PlaceableTuning.Default;

        var item = new ItemInstance(
            guid: DeterministicGuidGenerator.GenerateFromGuid(UninstalledItemGuidScope, placeable.Guid, currentTick),
            definitionId: def.Id,
            position: placeable.Position,
            z: placeable.Z,
            stackCount: 1,
            spawnTick: currentTick)
        {
            // Restore preserved properties
            MaterialId = placeable.SourceItemMaterial,
            QualityTier = placeable.SourceItemQuality,
            Improvements = placeable.SourceItemDecorations != null
                ? new List<Improvement>(placeable.SourceItemDecorations)
                : null,
            CraftedBy = placeable.SourceItemMaker,

            // Update condition based on placeable HP
            ConditionState = ComputeConditionState(placeable.HitPoints, placeable.MaxHitPoints, tuning)
        };

        return item;
    }

    private static string ComputeConditionState(int hp, int maxHp, PlaceableTuning tuning)
    {
        if (hp <= 0) return "Broken";
        var ratio = (float)hp / maxHp;

        // Use tuning thresholds (sorted from highest to lowest)
        var thresholds = tuning.ConditionThresholds.OrderByDescending(kv => kv.Value);
        foreach (var threshold in thresholds)
        {
            if (ratio >= threshold.Value)
            {
                // Capitalize first letter
                return char.ToUpper(threshold.Key[0]) + threshold.Key.Substring(1);
            }
        }

        return "Broken";
    }

    private static bool IsWorkshopDefinition(ConstructionDefinition def)
    {
        if (string.Equals(def.Category, "workshop", StringComparison.OrdinalIgnoreCase))
            return true;
        if (def.PlaceableProfile?.Tags == null) return false;
        return Array.IndexOf(def.PlaceableProfile.Tags, "workshop") >= 0;
    }
}

/// <summary>
/// Placeable kind (Installable from item, or Construction built on-site)
/// </summary>
public enum PlaceableKind
{
    /// <summary>
    /// Installable from item (has source_item_* fields, preserves quality/material/decorations)
    /// </summary>
    Installable,

    /// <summary>
    /// Built on-site from construction definition (no source item, quality always 0)
    /// </summary>
    Construction
}

/// <summary>
/// Door state component (only for placeables with passability=doorway)
/// </summary>
public sealed class DoorState
{
    /// <summary>
    /// Is door currently open (affects passability)
    /// </summary>
    public bool IsOpen { get; set; } = false;

    /// <summary>
    /// Is door locked (blocks opening)
    /// </summary>
    public bool IsLocked { get; set; } = false;
}

/// <summary>
/// Runtime state for a construction site placeable.
/// </summary>
public sealed class ConstructionSiteState
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
