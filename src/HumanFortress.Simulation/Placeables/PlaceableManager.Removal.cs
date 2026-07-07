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

        // Unsync and remove
        pd.UnsyncFromFurnitureCell(chunk, p, tick);
        pd.RemovePlaceable(idx);
        chunk.BumpConnectivityVersion();
        chunk.MarkTileDirty(idx, tick);
        return true;
    }
}
