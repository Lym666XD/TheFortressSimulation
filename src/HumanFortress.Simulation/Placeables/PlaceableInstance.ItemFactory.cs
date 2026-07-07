using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Simulation.Items;
using HumanFortress.Core.Random;
using HumanFortress.Simulation.Items;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Placeables;

internal sealed partial class PlaceableInstance
{
    /// <summary>
    /// Create placeable instance from installed item
    /// </summary>
    internal static PlaceableInstance CreateFromItem(
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
    /// Create item instance from uninstalled placeable (generates deterministic new GUID per SPEC §15.6)
    /// </summary>
    internal static ItemInstance CreateItemFromPlaceable(
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
}
