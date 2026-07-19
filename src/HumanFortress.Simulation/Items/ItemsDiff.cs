using System;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Items;

internal enum ItemsDiffOp
{
    AddItem = 1,
    RemoveItem = 2,
    SplitStack = 3,
}

/// <summary>
/// Immutable diff entry for Items layer operations (L5).
/// Applied around Simulation diffs: removals/splits before entity diffs, additions after terrain diffs.
/// </summary>
internal readonly struct ItemsDiff
{
    internal readonly ItemsDiffOp Op;
    internal readonly ChunkKey Chunk;
    internal readonly int LocalIndex; // 0..1023
    internal readonly string ItemId;
    internal readonly int Quantity;
    internal readonly int Priority;
    internal readonly string SystemId;
    internal readonly int LocalSeq;
    internal readonly Guid ItemGuid;
    internal readonly Guid NewItemGuid;
    internal readonly ReservationManager.ItemToken SourceReservation;
    internal readonly ReservationManager.ItemToken StagedReservation;

    internal ItemsDiff(
        ItemsDiffOp op,
        ChunkKey chunk,
        int localIndex,
        string itemId,
        int quantity,
        int priority,
        string systemId,
        int localSeq,
        Guid itemGuid = default,
        Guid newItemGuid = default,
        ReservationManager.ItemToken sourceReservation = default,
        ReservationManager.ItemToken stagedReservation = default)
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
        SourceReservation = sourceReservation;
        StagedReservation = stagedReservation;
    }

    internal long GetSortKey()
    {
        return SimulationDiffSortKeys.ByChunkCellPriorityDescending(
            Chunk.Z,
            Chunk.ChunkX,
            Chunk.ChunkY,
            LocalIndex,
            Priority,
            LocalSeq);
    }

    internal static int CompareDeterministic(ItemsDiff left, ItemsDiff right)
    {
        int result = left.Chunk.Z.CompareTo(right.Chunk.Z);
        if (result != 0) return result;
        result = left.Chunk.ChunkX.CompareTo(right.Chunk.ChunkX);
        if (result != 0) return result;
        result = left.Chunk.ChunkY.CompareTo(right.Chunk.ChunkY);
        if (result != 0) return result;
        result = left.LocalIndex.CompareTo(right.LocalIndex);
        if (result != 0) return result;
        result = left.Priority.CompareTo(right.Priority);
        if (result != 0) return result;
        result = left.Op.CompareTo(right.Op);
        if (result != 0) return result;
        result = left.ItemGuid.CompareTo(right.ItemGuid);
        if (result != 0) return result;
        result = left.NewItemGuid.CompareTo(right.NewItemGuid);
        if (result != 0) return result;
        result = string.Compare(left.SystemId, right.SystemId, StringComparison.Ordinal);
        if (result != 0) return result;
        return left.LocalSeq.CompareTo(right.LocalSeq);
    }
}
