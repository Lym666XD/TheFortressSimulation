using System;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Creatures;

/// <summary>
/// Runtime instance of a creature in the world
/// </summary>
internal sealed class CreatureInstance
{
    internal Guid Guid { get; }
    internal string DefinitionId { get; }
    internal string FactionId { get; set; }

    // Position
    internal Point Position { get; set; }
    internal int Z { get; set; }

    // Runtime state
    internal int HP { get; set; }
    internal int MaxHP { get; set; }
    internal ulong SpawnedAtTick { get; }

    internal CreatureInstance(Guid guid, string definitionId, string factionId, Point position, int z, int maxHP, ulong spawnTick)
    {
        Guid = guid;
        DefinitionId = definitionId;
        FactionId = factionId;
        Position = position;
        Z = z;
        HP = maxHP;
        MaxHP = maxHP;
        SpawnedAtTick = spawnTick;
    }
}
