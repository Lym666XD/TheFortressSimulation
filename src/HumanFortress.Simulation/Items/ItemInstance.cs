using System;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

/// <summary>
/// Runtime instance of an item in the world
/// </summary>
public sealed class ItemInstance
{
    public Guid Guid { get; }
    public string DefinitionId { get; }
    public string? MaterialId { get; set; }
    public int StackCount { get; set; }

    // Position
    public Point Position { get; set; }
    public int Z { get; set; }

    // Runtime state
    public int QualityTier { get; set; } = 0; // -3 to +3
    public string ConditionState { get; set; } = "Pristine";
    public ulong SpawnedAtTick { get; }

    // Job/haul transient state
    public bool IsReserved { get; set; } = false;
    public Guid? ReservedBy { get; set; } = null;
    public bool IsCarried { get; set; } = false;
    public Guid? CarriedBy { get; set; } = null;

    public ItemInstance(Guid guid, string definitionId, Point position, int z, int stackCount, ulong spawnTick)
    {
        Guid = guid;
        DefinitionId = definitionId;
        Position = position;
        Z = z;
        StackCount = stackCount;
        SpawnedAtTick = spawnTick;
    }
}
