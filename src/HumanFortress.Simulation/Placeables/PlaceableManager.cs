using System;
using System.Collections.Generic;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using WorldClass = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Manager for placeable operations across chunks.
/// Handles collision detection, cross-chunk footprints, and two-phase placement protocol.
/// </summary>
public sealed class PlaceableManager
{
    /// <summary>
    /// Check if placeable can be placed at position without collision.
    /// Validates full footprint across all chunks.
    /// </summary>
    public static CollisionResult CheckCollision(
        WorldClass world,
        Point position,
        int z,
        Footprint footprint)
    {
        var result = new CollisionResult { CanPlace = true };

        // Check each cell in footprint
        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                int worldX = position.X + dx;
                int worldY = position.Y + dy;

                // Get chunk for this cell
                int chunkX = worldX / Chunk.SIZE_XY;
                int chunkY = worldY / Chunk.SIZE_XY;
                var chunk = world.GetChunk(new ChunkKey(chunkX, chunkY, z));
                if (chunk == null)
                {
                    result.CanPlace = false;
                    result.FailureReason = $"Chunk not loaded at ({worldX}, {worldY}, {z})";
                    return result;
                }

                // Convert to local coordinates
                int localX = worldX % Chunk.SIZE_XY;
                int localY = worldY % Chunk.SIZE_XY;
                int localIndex = Chunk.LocalIndex(localX, localY);

                // Check if cell already has placeable
                var placeableData = chunk.GetPlaceableData();
                if (placeableData?.HasPlaceableAt(localIndex) == true)
                {
                    result.CanPlace = false;
                    result.FailureReason = $"Cell ({worldX}, {worldY}) already occupied";
                    result.BlockedCells.Add(new Point(worldX, worldY));
                }

                // Check if tile is walkable (basic placement rule)
                var tile = chunk.GetTile(localX, localY);
                if (!tile.IsWalkable)
                {
                    result.CanPlace = false;
                    result.FailureReason = $"Cell ({worldX}, {worldY}) is not walkable";
                    result.BlockedCells.Add(new Point(worldX, worldY));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Place placeable instance with full cross-chunk footprint handling.
    /// Uses two-phase protocol for deterministic cross-chunk writes.
    /// </summary>
    public static void PlacePlaceable(
        WorldClass world,
        PlaceableInstance placeable,
        ulong tick)
    {
        var footprint = placeable.Footprint;
        var position = placeable.Position;
        int z = placeable.Z;

        // Collect all affected chunks
        var affectedChunks = new HashSet<Chunk>();
        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                int worldX = position.X + dx;
                int worldY = position.Y + dy;
                int chunkX = worldX / Chunk.SIZE_XY;
                int chunkY = worldY / Chunk.SIZE_XY;
                var chunk = world.GetChunk(new ChunkKey(chunkX, chunkY, z));
                if (chunk != null)
                {
                    affectedChunks.Add(chunk);
                }
            }
        }

        // Determine primary chunk (owns the placeable instance)
        int primaryChunkX = position.X / Chunk.SIZE_XY;
        int primaryChunkY = position.Y / Chunk.SIZE_XY;
        var primaryChunk = world.GetChunk(new ChunkKey(primaryChunkX, primaryChunkY, z));
        if (primaryChunk == null)
            throw new InvalidOperationException($"Primary chunk not loaded at ({position.X}, {position.Y}, {z})");

        // Phase 1: Add placeable to primary chunk
        primaryChunk.EnsurePlaceableData();
        int primaryLocalX = position.X % Chunk.SIZE_XY;
        int primaryLocalY = position.Y % Chunk.SIZE_XY;
        int primaryLocalIndex = Chunk.LocalIndex(primaryLocalX, primaryLocalY);
        primaryChunk.GetPlaceableData()!.AddPlaceable(primaryLocalIndex, placeable);

        // Phase 2: Sync to FurnitureCell and add external refs for cross-chunk cells
        foreach (var chunk in affectedChunks)
        {
            chunk.EnsurePlaceableData();
            var placeableData = chunk.GetPlaceableData()!;

            if (chunk == primaryChunk)
            {
                // Primary chunk: sync to FurnitureCell
                placeableData.SyncToFurnitureCell(chunk, placeable, tick);
            }
            else
            {
                // Secondary chunk: add external refs
                for (int dy = 0; dy < footprint.D; dy++)
                {
                    for (int dx = 0; dx < footprint.W; dx++)
                    {
                        int worldX = position.X + dx;
                        int worldY = position.Y + dy;

                        // Check if this cell belongs to this secondary chunk
                        int cellChunkX = worldX / Chunk.SIZE_XY;
                        int cellChunkY = worldY / Chunk.SIZE_XY;
                        var cellChunk = world.GetChunk(new ChunkKey(cellChunkX, cellChunkY, z));
                        if (cellChunk == chunk)
                        {
                            int localX = worldX % Chunk.SIZE_XY;
                            int localY = worldY % Chunk.SIZE_XY;
                            int localIndex = Chunk.LocalIndex(localX, localY);
                            placeableData.AddExternalRef(localIndex, placeable.Guid);
                        }
                    }
                }
            }

            // Bump connectivity version for all affected chunks
            chunk.BumpConnectivityVersion();
            chunk.MarkTileDirty(0, tick); // Mark chunk dirty (index 0 is placeholder)
        }
    }

    /// <summary>
    /// Remove ghost placeable at anchor position if present.
    /// </summary>
    public static bool RemoveGhostAt(WorldClass world, SadRogue.Primitives.Point position, int z, string? purpose, ulong tick)
    {
        int cx = position.X / Chunk.SIZE_XY;
        int cy = position.Y / Chunk.SIZE_XY;
        int lx = position.X % Chunk.SIZE_XY;
        int ly = position.Y % Chunk.SIZE_XY;
        var ck = new ChunkKey(cx, cy, z);
        var chunk = world.GetChunk(ck);
        if (chunk == null) return false;
        var pd = chunk.GetPlaceableData();
        if (pd == null) return false;
        int idx = Chunk.LocalIndex(lx, ly);
        if (!pd.TryGetOwnedAt(idx, out var p)) return false;
        if (!p.IsGhost) return false;
        if (!string.IsNullOrEmpty(purpose) && (p.DefinitionId != $"core_construction_ghost:{purpose}")) return false;

        // Unsync and remove
        pd.UnsyncFromFurnitureCell(chunk, p, tick);
        pd.RemovePlaceable(idx);
        chunk.BumpConnectivityVersion();
        chunk.MarkTileDirty(idx, tick);
        return true;
    }

    /// <summary>
    /// Remove any owned placeable at the anchor position (regardless of ghost flag).
    /// Intended for removing construction sites upon completion.
    /// </summary>
    public static bool RemoveOwnedAt(WorldClass world, SadRogue.Primitives.Point position, int z, ulong tick)
    {
        int cx = position.X / Chunk.SIZE_XY;
        int cy = position.Y / Chunk.SIZE_XY;
        int lx = position.X % Chunk.SIZE_XY;
        int ly = position.Y % Chunk.SIZE_XY;
        var ck = new ChunkKey(cx, cy, z);
        var chunk = world.GetChunk(ck);
        if (chunk == null) return false;
        var pd = chunk.GetPlaceableData();
        if (pd == null) return false;
        int idx = Chunk.LocalIndex(lx, ly);
        if (!pd.TryGetOwnedAt(idx, out var p)) return false;

        // Unsync and remove
        pd.UnsyncFromFurnitureCell(chunk, p, tick);
        pd.RemovePlaceable(idx);
        chunk.BumpConnectivityVersion();
        chunk.MarkTileDirty(idx, tick);
        return true;
    }

    /// <summary>
    /// Get all chunks affected by placeable footprint.
    /// Used for cross-chunk collision detection and placement.
    /// </summary>
    public static IEnumerable<ChunkKey> GetAffectedChunks(Point position, int z, Footprint footprint)
    {
        var chunks = new HashSet<ChunkKey>();

        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                int worldX = position.X + dx;
                int worldY = position.Y + dy;

                int chunkX = worldX / Chunk.SIZE_XY;
                int chunkY = worldY / Chunk.SIZE_XY;

                chunks.Add(new ChunkKey(chunkX, chunkY, z));
            }
        }

        return chunks;
    }
}

/// <summary>
/// Result of collision detection
/// </summary>
public sealed class CollisionResult
{
    public bool CanPlace { get; set; }
    public string? FailureReason { get; set; }
    public List<Point> BlockedCells { get; set; } = new();
}
