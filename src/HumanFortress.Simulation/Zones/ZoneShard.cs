using System;
using System.Collections;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Zones;

/// <summary>
/// Per-chunk fragment of a zone per ZONE_SPEC.md §4.3.
/// Maintains local member cells and candidate furniture caches.
/// </summary>
internal sealed class ZoneShard
{
    /// <summary>
    /// Zone ID this shard belongs to.
    /// </summary>
    internal int ZoneId { get; }

    /// <summary>
    /// Chunk this shard is in.
    /// </summary>
    internal ChunkKey ChunkKey { get; }

    /// <summary>
    /// Member cells in this chunk (bitset for 32x32=1024 cells).
    /// </summary>
    internal BitArray MemberCells { get; }

    /// <summary>
    /// Number of member cells in this shard.
    /// </summary>
    internal int CellCount { get; private set; }

    internal ZoneShard(int zoneId, ChunkKey chunkKey)
    {
        ZoneId = zoneId;
        ChunkKey = chunkKey;
        MemberCells = new BitArray(Chunk.CELLS_PER_LAYER);
        CellCount = 0;
    }

    /// <summary>
    /// Add cells to this shard.
    /// </summary>
    internal void AddCells(System.Collections.Generic.IEnumerable<int> cellIndices)
    {
        foreach (var idx in cellIndices)
        {
            if (idx >= 0 && idx < Chunk.CELLS_PER_LAYER && !MemberCells[idx])
            {
                MemberCells[idx] = true;
                CellCount++;
            }
        }
    }

    /// <summary>
    /// Remove cells from this shard.
    /// </summary>
    internal void RemoveCells(System.Collections.Generic.IEnumerable<int> cellIndices)
    {
        foreach (var idx in cellIndices)
        {
            if (idx >= 0 && idx < Chunk.CELLS_PER_LAYER && MemberCells[idx])
            {
                MemberCells[idx] = false;
                CellCount--;
            }
        }
    }

    /// <summary>
    /// Check if a cell is part of this shard.
    /// </summary>
    internal bool ContainsCell(int cellIndex)
    {
        return cellIndex >= 0 && cellIndex < Chunk.CELLS_PER_LAYER && MemberCells[cellIndex];
    }

    /// <summary>
    /// Clear all cells.
    /// </summary>
    internal void Clear()
    {
        MemberCells.SetAll(false);
        CellCount = 0;
    }
}
