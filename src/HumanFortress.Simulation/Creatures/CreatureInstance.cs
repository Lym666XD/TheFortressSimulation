using System;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Creatures;

/// <summary>
/// Runtime instance of a creature in the world
/// </summary>
public sealed class CreatureInstance
{
    public Guid Guid { get; }
    public string DefinitionId { get; }
    public string FactionId { get; set; }

    // Position
    public Point Position { get; set; }
    public int Z { get; set; }

    // Runtime state
    public int HP { get; set; }
    public int MaxHP { get; set; }
    public ulong SpawnedAtTick { get; }

    public CreatureInstance(Guid guid, string definitionId, string factionId, Point position, int z, int maxHP, ulong spawnTick)
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