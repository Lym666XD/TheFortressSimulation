using System;
using System.Collections.Generic;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Zones;

/// <summary>
/// Coordinator for zone operations at the World level.
/// Provides high-level methods for zone creation, deletion, and cell management.
/// </summary>
public sealed class ZoneCoordinator
{
    private readonly World.World _world;
    private readonly ZoneManager _manager;

    public ZoneCoordinator(World.World world, ZoneManager manager)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    /// <summary>
    /// Access to the underlying ZoneManager.
    /// </summary>
    public ZoneManager Manager => _manager;

    /// <summary>
    /// Create a zone from a world-space rectangle at a given Z level.
    /// </summary>
    public int CreateZoneFromRect(string defId, string name, Rectangle worldRect, int z, ulong currentTick)
    {
        // Calculate home chunk from rectangle center
        var centerX = worldRect.X + worldRect.Width / 2;
        var centerY = worldRect.Y + worldRect.Height / 2;
        var homeChunk = new ChunkKey(centerX / Chunk.SIZE_XY, centerY / Chunk.SIZE_XY, z);

        // Create zone instance
        var zoneId = _manager.CreateZone(defId, name, homeChunk, currentTick);

        // Add cells to the zone
        AddCellsToZone(zoneId, worldRect, z);

        return zoneId;
    }

    /// <summary>
    /// Add cells from a rectangle to an existing zone.
    /// </summary>
    public void AddCellsToZone(int zoneId, Rectangle worldRect, int z)
    {
        var zone = _manager.GetZone(zoneId);
        if (zone == null)
            return;

        var affectedChunks = new HashSet<ChunkKey>();

        // Iterate through rectangle and add cells
        for (int wx = worldRect.X; wx < worldRect.X + worldRect.Width; wx++)
        {
            for (int wy = worldRect.Y; wy < worldRect.Y + worldRect.Height; wy++)
            {
                if (!_world.IsValidPosition(wx, wy, z))
                    continue;

                var chunkX = wx / Chunk.SIZE_XY;
                var chunkY = wy / Chunk.SIZE_XY;
                var chunkKey = new ChunkKey(chunkX, chunkY, z);
                var localX = wx % Chunk.SIZE_XY;
                var localY = wy % Chunk.SIZE_XY;
                var cellIndex = Chunk.LocalIndex(localX, localY);

                // Get or create chunk
                var chunk = _world.GetOrCreateChunk(chunkKey);
                var zoneData = chunk.GetZoneData();
                if (zoneData == null)
                {
                    chunk.EnsureZoneData();
                    zoneData = chunk.GetZoneData();
                }

                // Create shard if needed
                zoneData!.CreateOrUpdateShard(zoneId, chunkKey);

                // Add cell to shard
                zoneData.AddCellsToZone(zoneId, new[] { cellIndex });

                affectedChunks.Add(chunkKey);
            }
        }

        // Update zone's member chunks
        zone.UpdateMemberChunks(affectedChunks);
        zone.TotalCells += worldRect.Width * worldRect.Height;
    }

    /// <summary>
    /// Remove cells from a rectangle from an existing zone.
    /// </summary>
    public void RemoveCellsFromZone(int zoneId, Rectangle worldRect, int z)
    {
        var zone = _manager.GetZone(zoneId);
        if (zone == null)
            return;

        var affectedChunks = new HashSet<ChunkKey>();

        // Iterate through rectangle and remove cells
        for (int wx = worldRect.X; wx < worldRect.X + worldRect.Width; wx++)
        {
            for (int wy = worldRect.Y; wy < worldRect.Y + worldRect.Height; wy++)
            {
                if (!_world.IsValidPosition(wx, wy, z))
                    continue;

                var chunkX = wx / Chunk.SIZE_XY;
                var chunkY = wy / Chunk.SIZE_XY;
                var chunkKey = new ChunkKey(chunkX, chunkY, z);
                var localX = wx % Chunk.SIZE_XY;
                var localY = wy % Chunk.SIZE_XY;
                var cellIndex = Chunk.LocalIndex(localX, localY);

                var chunk = _world.GetChunk(chunkKey);
                if (chunk == null)
                    continue;

                var zoneData = chunk.GetZoneData();
                if (zoneData == null)
                    continue;

                // Remove cell from shard
                zoneData.RemoveCellsFromZone(zoneId, new[] { cellIndex });

                affectedChunks.Add(chunkKey);
            }
        }

        // Update zone's member chunks
        var remainingChunks = new HashSet<ChunkKey>();
        foreach (var chunkKey in affectedChunks)
        {
            var chunk = _world.GetChunk(chunkKey);
            var zoneData = chunk?.GetZoneData();
            var shard = zoneData?.GetShard(zoneId);
            if (shard != null && shard.CellCount > 0)
            {
                remainingChunks.Add(chunkKey);
            }
        }

        zone.UpdateMemberChunks(remainingChunks);
        zone.TotalCells = Math.Max(0, zone.TotalCells - worldRect.Width * worldRect.Height);
    }

    /// <summary>
    /// Delete a zone entirely, removing it from all chunks.
    /// </summary>
    public void DeleteZone(int zoneId)
    {
        var zone = _manager.GetZone(zoneId);
        if (zone == null)
            return;

        // Remove from all member chunks
        foreach (var chunkKey in zone.MemberChunks)
        {
            var chunk = _world.GetChunk(chunkKey);
            var zoneData = chunk?.GetZoneData();
            zoneData?.DeleteShard(zoneId);
        }

        // Remove from manager
        _manager.DeleteZone(zoneId);
    }

    /// <summary>
    /// Get zone ID at a specific world position.
    /// </summary>
    public int GetZoneAtPosition(int worldX, int worldY, int z)
    {
        if (!_world.IsValidPosition(worldX, worldY, z))
            return 0;

        var chunkX = worldX / Chunk.SIZE_XY;
        var chunkY = worldY / Chunk.SIZE_XY;
        var chunkKey = new ChunkKey(chunkX, chunkY, z);
        var localX = worldX % Chunk.SIZE_XY;
        var localY = worldY % Chunk.SIZE_XY;
        var cellIndex = Chunk.LocalIndex(localX, localY);

        var chunk = _world.GetChunk(chunkKey);
        var zoneData = chunk?.GetZoneData();
        return zoneData?.GetZoneAtCell(cellIndex) ?? 0;
    }
}
