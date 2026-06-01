using System;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Items;

public enum ItemsDiffOp
{
    AddItem = 1,
    RemoveItem = 2,
    SplitStack = 3,
}

/// <summary>
/// Immutable diff entry for Items layer operations (L5).
/// Applied around Simulation diffs: removals/splits before entity diffs, additions after terrain diffs.
/// </summary>
public readonly struct ItemsDiff
{
    public readonly ItemsDiffOp Op;
    public readonly ChunkKey Chunk;
    public readonly int LocalIndex; // 0..1023
    public readonly string ItemId;
    public readonly int Quantity;
    public readonly int Priority;
    public readonly string SystemId;
    public readonly int LocalSeq;
    public readonly Guid ItemGuid;
    public readonly Guid NewItemGuid;

    public ItemsDiff(
        ItemsDiffOp op,
        ChunkKey chunk,
        int localIndex,
        string itemId,
        int quantity,
        int priority,
        string systemId,
        int localSeq,
        Guid itemGuid = default,
        Guid newItemGuid = default)
    {
        Op = op;
        Chunk = chunk;
        LocalIndex = localIndex;
        ItemId = itemId;
        Quantity = quantity;
        Priority = priority;
        SystemId = systemId;
        LocalSeq = localSeq;
        ItemGuid = itemGuid;
        NewItemGuid = newItemGuid;
    }

    public long GetSortKey()
    {
        // [chunkZ:10][chunkX:10][chunkY:10][localIndex:16][priority:8][localSeq:10]
        long key = 0;
        key |= ((long)(Chunk.Z & 0x3FF)) << 54;
        key |= ((long)(Chunk.ChunkX & 0x3FF)) << 44;
        key |= ((long)(Chunk.ChunkY & 0x3FF)) << 34;
        key |= ((long)(LocalIndex & 0xFFFF)) << 18;
        key |= ((long)(255 - (Priority & 0xFF))) << 10; // invert for desc priority
        key |= (long)(LocalSeq & 0x3FF);
        return key;
    }
}
