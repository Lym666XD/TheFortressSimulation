using System;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Core.Random;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Factory helpers for placeable instances.
/// </summary>
public static class PlaceableFactory
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
}

