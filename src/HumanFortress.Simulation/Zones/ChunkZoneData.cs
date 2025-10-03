using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Zones;

/// <summary>
/// Per-chunk zone data including zone shards per ZONE_SPEC.md §4.3.
/// Thread-safe for reads during Read phase, mutations only in Write phase.
/// </summary>
public sealed class ChunkZoneData
{
    private readonly Dictionary<int, ZoneShard> _shards = new();
    private readonly int[] _cellZones; // zoneId per cell, 0=none
    private readonly object _readLock = new();

    /// <summary>
    /// Dirty generation for cache invalidation.
    /// </summary>
    public uint DirtyGeneration { get; private set; }

    public ChunkZoneData()
    {
        _cellZones = new int[Chunk.CELLS_PER_LAYER];
        DirtyGeneration = 1;
    }

    #region Read Operations (Thread-safe)

    /// <summary>
    /// Get zone ID at a specific cell (Read-safe).
    /// </summary>
    public int GetZoneAtCell(int cellIndex)
    {
        if (cellIndex < 0 || cellIndex >= Chunk.CELLS_PER_LAYER)
            return 0;

        lock (_readLock)
        {
            return _cellZones[cellIndex];
        }
    }

    /// <summary>
    /// Get shard for a zone (Read-safe).
    /// </summary>
    public ZoneShard? GetShard(int zoneId)
    {
        lock (_readLock)
        {
            return _shards.GetValueOrDefault(zoneId);
        }
    }

    /// <summary>
    /// Get all shards in this chunk (Read-safe).
    /// </summary>
    public IEnumerable<ZoneShard> GetAllShards()
    {
        lock (_readLock)
        {
            return _shards.Values.ToList();
        }
    }

    /// <summary>
    /// Check if zone has any cells in this chunk.
    /// </summary>
    public bool HasZone(int zoneId)
    {
        lock (_readLock)
        {
            return _shards.ContainsKey(zoneId);
        }
    }

    #endregion

    #region Write Operations (Write phase only)

    /// <summary>
    /// Create or update a zone shard (Write phase only).
    /// </summary>
    public void CreateOrUpdateShard(int zoneId, ChunkKey chunkKey)
    {
        if (!_shards.ContainsKey(zoneId))
        {
            _shards[zoneId] = new ZoneShard(zoneId, chunkKey);
            DirtyGeneration++;
        }
    }

    /// <summary>
    /// Add cells to a zone (Write phase only).
    /// </summary>
    public void AddCellsToZone(int zoneId, IEnumerable<int> cellIndices)
    {
        if (!_shards.TryGetValue(zoneId, out var shard))
            return;

        foreach (var idx in cellIndices)
        {
            if (idx >= 0 && idx < Chunk.CELLS_PER_LAYER)
            {
                _cellZones[idx] = zoneId;
            }
        }

        shard.AddCells(cellIndices);
        DirtyGeneration++;
    }

    /// <summary>
    /// Remove cells from a zone (Write phase only).
    /// </summary>
    public void RemoveCellsFromZone(int zoneId, IEnumerable<int> cellIndices)
    {
        if (!_shards.TryGetValue(zoneId, out var shard))
            return;

        foreach (var idx in cellIndices)
        {
            if (idx >= 0 && idx < Chunk.CELLS_PER_LAYER && _cellZones[idx] == zoneId)
            {
                _cellZones[idx] = 0;
            }
        }

        shard.RemoveCells(cellIndices);
        DirtyGeneration++;
    }

    /// <summary>
    /// Delete a zone shard (Write phase only).
    /// </summary>
    public void DeleteShard(int zoneId)
    {
        if (!_shards.TryGetValue(zoneId, out var shard))
            return;

        // Clear cell assignments
        for (int i = 0; i < Chunk.CELLS_PER_LAYER; i++)
        {
            if (_cellZones[i] == zoneId)
                _cellZones[i] = 0;
        }

        _shards.Remove(zoneId);
        DirtyGeneration++;
    }

    #endregion
}
