using System;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Random;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Factory helpers for placeable instances.
/// </summary>
internal static class PlaceableFactory
{
    /// <summary>
    /// Create a non-blocking construction ghost (1x1) at position for a planned L0 build.
    /// Deterministic GUID by position + tickSeed.
    /// </summary>
    public static PlaceableInstance CreateConstructionGhost(Point worldPos, int z, ulong tickSeed, string purpose)
    {
        var guid = DeterministicGuidGenerator.GenerateFromPosition(tickSeed, worldPos.X, worldPos.Y, z);
        var fp = new Footprint(1, 1, 1);
        var inst = new PlaceableInstance(
            guid,
            PlaceableKind.Construction,
            definitionId: $"core_construction_ghost:{purpose}",
            position: worldPos,
            z: z,
            footprint: fp)
        {
            IsGhost = true,
            Passability = PassabilityMode.Nonblocking,
            Effects = new EffectsBlock { Beauty = 0, Comfort = 0 }
        };
        return inst;
    }

    /// <summary>
    /// Create a construction site placeable (non-blocking footprint by default) with site state.
    /// </summary>
    public static PlaceableInstance CreateConstructionSite(Point worldPos, int z, ulong tickSeed, string targetId, Footprint fp,
        IReadOnlyDictionary<string, int> materialsRequired, int totalBuildTicks)
    {
        var guid = DeterministicGuidGenerator.GenerateFromPosition(tickSeed, worldPos.X, worldPos.Y, z);
        var inst = new PlaceableInstance(
            guid,
            PlaceableKind.Construction,
            definitionId: $"core_construction_site:{targetId}",
            position: worldPos,
            z: z,
            footprint: fp)
        {
            IsGhost = false,
            Passability = PassabilityMode.Nonblocking,
            Effects = new EffectsBlock { Beauty = 0, Comfort = 0 },
            ConstructionSite = new ConstructionSiteState
            {
                TargetId = targetId,
                MaterialsRequired = new Dictionary<string, int>(materialsRequired),
                MaterialsDelivered = new Dictionary<string, int>(),
                BuildProgressTicks = 0,
                TotalBuildTicks = totalBuildTicks
            }
        };
        return inst;
    }
}
