using System;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Random;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Placeables;

internal sealed partial class PlaceableInstance
{
    /// <summary>
    /// Create placeable instance from construction
    /// </summary>
    internal static PlaceableInstance CreateFromConstruction(
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

    private static bool IsWorkshopDefinition(ConstructionDefinition def)
    {
        if (string.Equals(def.Category, "workshop", StringComparison.OrdinalIgnoreCase))
            return true;
        if (def.PlaceableProfile?.Tags == null) return false;
        return Array.IndexOf(def.PlaceableProfile.Tags, "workshop") >= 0;
    }
}
