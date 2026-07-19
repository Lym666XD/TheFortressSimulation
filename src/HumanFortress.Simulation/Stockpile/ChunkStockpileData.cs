using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Per-chunk stockpile data including zone shards and item indexing.
/// Thread-safe for reads during Read phase, mutations only in Write phase per STOCKPILE_SPEC.md.
/// </summary>
internal sealed partial class ChunkStockpileData
{
    private readonly Dictionary<int, ZoneShard> _shards = new();
    private readonly int[] _cellZones; // zoneId per cell, 0=none
    private readonly Dictionary<string, List<ulong>> _itemsByTag = new();
    private readonly Dictionary<int, List<ulong>> _itemsByZone = new();
    private readonly List<ulong> _looseItems = new(); // not in any zone
    private readonly object _readLock = new();

    /// <summary>
    /// Dirty generation for cache invalidation.
    /// </summary>
    internal uint DirtyGeneration { get; private set; }

    internal ChunkStockpileData()
    {
        _cellZones = new int[Chunk.CELLS_PER_LAYER];
        DirtyGeneration = 1;
    }

    #region Read Operations (Thread-safe)

    /// <summary>
    /// Get zones at a specific cell (Read-safe).
    /// </summary>
    internal int GetZoneAtCell(int cellIndex)
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
    internal ZoneShard? GetShard(int zoneId)
    {
        lock (_readLock)
        {
            return _shards.GetValueOrDefault(zoneId);
        }
    }

    /// <summary>
    /// Get all shards in this chunk (Read-safe).
    /// </summary>
    internal IEnumerable<ZoneShard> GetAllShards()
    {
        lock (_readLock)
        {
            return _shards
                .OrderBy(static entry => entry.Key)
                .Select(static entry => entry.Value)
                .ToList();
        }
    }

    /// <summary>
    /// Get items by tag (Read-safe).
    /// </summary>
    internal IEnumerable<ulong> GetItemsByTag(string tag)
    {
        lock (_readLock)
        {
            return _itemsByTag.TryGetValue(tag, out var items)
                ? items.OrderBy(static handle => handle).ToList()
                : Enumerable.Empty<ulong>();
        }
    }

    /// <summary>
    /// Get items in a zone (Read-safe).
    /// </summary>
    internal IEnumerable<ulong> GetItemsInZone(int zoneId)
    {
        lock (_readLock)
        {
            return _itemsByZone.TryGetValue(zoneId, out var items)
                ? items.OrderBy(static handle => handle).ToList()
                : Enumerable.Empty<ulong>();
        }
    }

    /// <summary>
    /// Get loose items not in any zone (Read-safe).
    /// </summary>
    internal IEnumerable<ulong> GetLooseItems()
    {
        lock (_readLock)
        {
            return _looseItems
                .OrderBy(static handle => handle)
                .ToList();
        }
    }

    #endregion

    #region Write Operations (Write phase only)

    /// <summary>
    /// Create or update a zone shard (Write phase only).
    /// </summary>
    internal void CreateOrUpdateShard(int zoneId, ChunkKey chunkKey)
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
    internal void AddCellsToZone(int zoneId, IEnumerable<int> cellIndices)
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
    internal void RemoveCellsFromZone(int zoneId, IEnumerable<int> cellIndices)
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
    internal void DeleteShard(int zoneId)
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

        // Move items from zone to loose
        if (_itemsByZone.TryGetValue(zoneId, out var items))
        {
            _looseItems.AddRange(items);
            _itemsByZone.Remove(zoneId);
        }

        DirtyGeneration++;
    }

    /// <summary>
    /// Update item placement (Write phase only).
    /// </summary>
    internal void OnItemPlaced(int itemHandle, int cellIndex, int zoneId, List<string> tags)
    {
        OnItemPlaced(unchecked((uint)itemHandle), cellIndex, zoneId, tags);
    }

    internal void OnItemPlaced(ulong itemHandle, int cellIndex, int zoneId, List<string> tags)
    {
        // Remove from loose if present
        _looseItems.Remove(itemHandle);

        // Add to zone index
        if (zoneId > 0)
        {
            if (!_itemsByZone.TryGetValue(zoneId, out var zoneItems))
            {
                zoneItems = new List<ulong>();
                _itemsByZone[zoneId] = zoneItems;
            }

            bool addedToZone = !zoneItems.Contains(itemHandle);
            if (addedToZone)
                zoneItems.Add(itemHandle);

            // Update shard counts
            if (addedToZone && _shards.TryGetValue(zoneId, out var shard))
            {
                shard.OccupySlot();
            }
        }
        else
        {
            if (!_looseItems.Contains(itemHandle))
                _looseItems.Add(itemHandle);
        }

        // Update tag index
        foreach (var tag in tags)
        {
            if (!_itemsByTag.TryGetValue(tag, out var taggedItems))
            {
                taggedItems = new List<ulong>();
                _itemsByTag[tag] = taggedItems;
            }

            if (!taggedItems.Contains(itemHandle))
                taggedItems.Add(itemHandle);
        }

        DirtyGeneration++;
    }

    /// <summary>
    /// Update item removal (Write phase only).
    /// </summary>
    internal void OnItemRemoved(int itemHandle, int cellIndex, int zoneId, List<string> tags)
    {
        OnItemRemoved(unchecked((uint)itemHandle), cellIndex, zoneId, tags);
    }

    internal void OnItemRemoved(ulong itemHandle, int cellIndex, int zoneId, List<string> tags)
    {
        // Remove from zone index
        if (zoneId > 0)
        {
            bool removedFromZone = false;
            if (_itemsByZone.TryGetValue(zoneId, out var zoneItems))
            {
                removedFromZone = zoneItems.Remove(itemHandle);
                if (zoneItems.Count == 0)
                    _itemsByZone.Remove(zoneId);
            }

            // Update shard counts
            if (removedFromZone && _shards.TryGetValue(zoneId, out var shard))
            {
                shard.FreeSlot();
            }
        }
        else
        {
            _looseItems.Remove(itemHandle);
        }

        // Update tag index
        foreach (var tag in tags)
        {
            if (!_itemsByTag.TryGetValue(tag, out var taggedItems))
                continue;

            taggedItems.Remove(itemHandle);
            if (taggedItems.Count == 0)
                _itemsByTag.Remove(tag);
        }

        DirtyGeneration++;
    }

    /// <summary>
    /// Reserve a slot in a zone (Write phase only).
    /// </summary>
    internal bool TryReserveSlot(int zoneId)
    {
        if (_shards.TryGetValue(zoneId, out var shard))
        {
            return shard.TryReserveSlot();
        }
        return false;
    }

    /// <summary>
    /// Release a reserved slot (Write phase only).
    /// </summary>
    internal void ReleaseSlot(int zoneId)
    {
        if (_shards.TryGetValue(zoneId, out var shard))
        {
            shard.ReleaseSlot();
        }
    }

    #endregion
}
