using System;
using System.Linq;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;
using WorldClass = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Placeables;

internal sealed partial class PlaceableManager
{
    /// <summary>
    /// Place placeable instance with full cross-chunk footprint handling.
    /// Uses two-phase protocol for deterministic cross-chunk writes.
    /// </summary>
    internal static void PlacePlaceable(
        WorldClass world,
        PlaceableInstance placeable,
        ulong tick)
    {
        var footprint = placeable.Footprint;
        var position = placeable.Position;
        int z = placeable.Z;

        // Collect all affected chunks in stable spatial order.
        var affectedChunks = GetAffectedChunks(position, z, footprint)
            .Select(world.GetChunk)
            .Where(static chunk => chunk != null)
            .Select(static chunk => chunk!)
            .ToArray();

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
            MarkFootprintCellsDirtyForChunk(chunk, position, z, footprint, tick);
        }
    }

    private static void MarkFootprintCellsDirtyForChunk(
        Chunk chunk,
        SadRogue.Primitives.Point position,
        int z,
        Footprint footprint,
        ulong tick)
    {
        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                int worldX = position.X + dx;
                int worldY = position.Y + dy;
                int chunkX = worldX / Chunk.SIZE_XY;
                int chunkY = worldY / Chunk.SIZE_XY;
                if (chunk.Key.ChunkX != chunkX || chunk.Key.ChunkY != chunkY || chunk.Key.Z != z)
                    continue;

                int localX = worldX % Chunk.SIZE_XY;
                int localY = worldY % Chunk.SIZE_XY;
                chunk.MarkTileDirty(Chunk.LocalIndex(localX, localY), tick);
            }
        }
    }
}
