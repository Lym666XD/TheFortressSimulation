using System;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using WorldClass = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Placeables;

internal sealed partial class PlaceableManager
{
    /// <summary>
    /// Resolve the placeable occupying a world cell, including cross-chunk
    /// external references owned by another chunk.
    /// </summary>
    internal static bool TryGetPlaceableAt(
        WorldClass world,
        Point position,
        int z,
        out PlaceableInstance? placeable)
    {
        placeable = null;

        var chunk = GetChunkForCell(world, position.X, position.Y, z, out var localIndex);
        var data = chunk?.GetPlaceableData();
        if (data == null)
            return false;

        if (data.TryGetOwnedAt(localIndex, out var owned))
        {
            placeable = owned;
            return true;
        }

        if (!data.TryGetExternalRefAt(localIndex, out var ownerGuid))
            return false;

        return TryGetOwnedPlaceableByGuid(world, ownerGuid, out placeable);
    }

    internal static bool TryGetOwnedPlaceableByGuid(
        WorldClass world,
        Guid placeableGuid,
        out PlaceableInstance? placeable)
    {
        foreach (var chunk in world.GetAllChunks())
        {
            var data = chunk.GetPlaceableData();
            if (data == null)
                continue;

            foreach (var entry in data.GetOwnedPlaceableSnapshot())
            {
                if (entry.Placeable.Guid == placeableGuid)
                {
                    placeable = entry.Placeable;
                    return true;
                }
            }
        }

        placeable = null;
        return false;
    }

    private static Chunk? GetChunkForCell(
        WorldClass world,
        int worldX,
        int worldY,
        int z,
        out int localIndex)
    {
        var chunkX = worldX / Chunk.SIZE_XY;
        var chunkY = worldY / Chunk.SIZE_XY;
        var localX = worldX % Chunk.SIZE_XY;
        var localY = worldY % Chunk.SIZE_XY;
        localIndex = Chunk.LocalIndex(localX, localY);
        return world.GetChunk(new ChunkKey(chunkX, chunkY, z));
    }
}
