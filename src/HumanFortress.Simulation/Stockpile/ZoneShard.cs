using System;
using System.Collections;
using System.Collections.Generic;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Per-chunk fragment of a stockpile zone.
/// Maintains local member cells and capacity tracking per STOCKPILE_SPEC.md.
/// </summary>
public sealed class ZoneShard
{
    /// <summary>
    /// Zone ID this shard belongs to.
    /// </summary>
    public int ZoneId { get; }

    /// <summary>
    /// Chunk this shard is in.
    /// </summary>
    public ChunkKey ChunkKey { get; }

    /// <summary>
    /// Member cells in this chunk (bitset for 32x32=1024 cells).
    /// </summary>
    public BitArray MemberCells { get; }

    /// <summary>
    /// Number of cells that can hold items.
    /// </summary>
    public int Capacity { get; private set; }

    /// <summary>
    /// Number of cells currently occupied.
    /// </summary>
    public int UsedSlots { get; private set; }

    /// <summary>
    /// Number of cells reserved for incoming items.
    /// </summary>
    public int ReservedSlots { get; private set; }

    /// <summary>
    /// Number of items in transit to this shard.
    /// </summary>
    public int IncomingCount { get; private set; }

    public ZoneShard(int zoneId, ChunkKey chunkKey)
    {
        ZoneId = zoneId;
        ChunkKey = chunkKey;
        MemberCells = new BitArray(Chunk.CELLS_PER_LAYER);
        Capacity = 0;
        UsedSlots = 0;
        ReservedSlots = 0;
        IncomingCount = 0;
    }

    /// <summary>
    /// Add cells to this shard.
    /// </summary>
    public void AddCells(IEnumerable<int> cellIndices)
    {
        foreach (var idx in cellIndices)
        {
            if (idx >= 0 && idx < Chunk.CELLS_PER_LAYER && !MemberCells[idx])
            {
                MemberCells[idx] = true;
                Capacity++;
            }
        }
    }

    /// <summary>
    /// Remove cells from this shard.
    /// </summary>
    public void RemoveCells(IEnumerable<int> cellIndices)
    {
        foreach (var idx in cellIndices)
        {
            if (idx >= 0 && idx < Chunk.CELLS_PER_LAYER && MemberCells[idx])
            {
                MemberCells[idx] = false;
                Capacity--;
            }
        }
    }

    /// <summary>
    /// Check if a cell is part of this shard.
    /// </summary>
    public bool ContainsCell(int cellIndex)
    {
        return cellIndex >= 0 && cellIndex < Chunk.CELLS_PER_LAYER && MemberCells[cellIndex];
    }

    /// <summary>
    /// Reserve a slot for an incoming item.
    /// </summary>
    public bool TryReserveSlot()
    {
        if (UsedSlots + ReservedSlots < Capacity)
        {
            ReservedSlots++;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Release a reserved slot.
    /// </summary>
    public void ReleaseSlot()
    {
        if (ReservedSlots > 0)
            ReservedSlots--;
    }

    /// <summary>
    /// Mark a slot as occupied.
    /// </summary>
    public void OccupySlot()
    {
        UsedSlots++;
        if (ReservedSlots > 0)
            ReservedSlots--;
    }

    /// <summary>
    /// Mark a slot as free.
    /// </summary>
    public void FreeSlot()
    {
        if (UsedSlots > 0)
            UsedSlots--;
    }

    /// <summary>
    /// Update incoming count.
    /// </summary>
    public void UpdateIncoming(int delta)
    {
        IncomingCount = Math.Max(0, IncomingCount + delta);
    }

    /// <summary>
    /// Get fill ratio for this shard.
    /// </summary>
    public float GetFillRatio()
    {
        if (Capacity == 0)
            return 0f;
        return (float)(UsedSlots + ReservedSlots) / Capacity;
    }

    /// <summary>
    /// Get available capacity.
    /// </summary>
    public int GetAvailableCapacity()
    {
        return Math.Max(0, Capacity - UsedSlots - ReservedSlots);
    }
}