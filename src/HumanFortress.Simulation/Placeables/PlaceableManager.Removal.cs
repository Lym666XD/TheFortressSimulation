using System.Collections.Generic;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using WorldClass = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Placeables;

internal sealed partial class PlaceableManager
{
    /// <summary>
    /// Remove ghost placeable at anchor position if present.
    /// </summary>
    internal static bool RemoveGhostAt(WorldClass world, Point position, int z, string? purpose, ulong tick)
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

        // Unsync primary owned state and remove secondary external references.
        pd.UnsyncFromFurnitureCell(chunk, p, tick);
        pd.RemovePlaceable(idx);
        RemoveExternalReferences(world, p, primaryChunk: chunk, tick);
        chunk.BumpConnectivityVersion();
        MarkFootprintCellsDirtyForChunk(chunk, p.Position, p.Z, p.Footprint, tick);
        return true;
    }

    /// <summary>
    /// Remove any owned placeable at the anchor position (regardless of ghost flag).
    /// Intended for removing construction sites upon completion.
    /// </summary>
    internal static bool RemoveOwnedAt(WorldClass world, Point position, int z, ulong tick)
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

        // Unsync primary owned state and remove secondary external references.
        pd.UnsyncFromFurnitureCell(chunk, p, tick);
        pd.RemovePlaceable(idx);
        RemoveExternalReferences(world, p, primaryChunk: chunk, tick);
        chunk.BumpConnectivityVersion();
        MarkFootprintCellsDirtyForChunk(chunk, p.Position, p.Z, p.Footprint, tick);
        return true;
    }

    private static void RemoveExternalReferences(WorldClass world, PlaceableInstance placeable, Chunk primaryChunk, ulong tick)
    {
        foreach (var chunkKey in GetAffectedChunks(placeable.Position, placeable.Z, placeable.Footprint))
        {
            var chunk = world.GetChunk(chunkKey);
            if (chunk == null || chunk == primaryChunk)
                continue;

            var data = chunk.GetPlaceableData();
            if (data == null)
                continue;

            var removedAny = false;
            foreach (var localIndex in EnumerateFootprintLocalIndexesForChunk(chunk, placeable))
            {
                if (data.TryGetExternalRefAt(localIndex, out var ownerGuid)
                    && ownerGuid == placeable.Guid
                    && data.RemoveExternalRef(localIndex))
                {
                    removedAny = true;
                }
            }

            if (!removedAny)
                continue;

            chunk.BumpConnectivityVersion();
            MarkFootprintCellsDirtyForChunk(chunk, placeable.Position, placeable.Z, placeable.Footprint, tick);
        }
    }

    private static IEnumerable<int> EnumerateFootprintLocalIndexesForChunk(Chunk chunk, PlaceableInstance placeable)
    {
        var footprint = placeable.Footprint;
        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                var worldX = placeable.Position.X + dx;
                var worldY = placeable.Position.Y + dy;
                var chunkX = worldX / Chunk.SIZE_XY;
                var chunkY = worldY / Chunk.SIZE_XY;
                if (chunk.Key.ChunkX != chunkX || chunk.Key.ChunkY != chunkY || chunk.Key.Z != placeable.Z)
                    continue;

                var localX = worldX % Chunk.SIZE_XY;
                var localY = worldY % Chunk.SIZE_XY;
                yield return Chunk.LocalIndex(localX, localY);
            }
        }
    }
}
