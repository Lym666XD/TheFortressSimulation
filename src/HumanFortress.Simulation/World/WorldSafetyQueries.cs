using System.Collections.Generic;

namespace HumanFortress.Simulation.World;

internal readonly record struct WorldPoint3(int X, int Y, int Z);

internal static class WorldSafetyQueries
{
    public static WorldPoint3? FindNearestStandableNonConstructionSite(
        World world,
        int startX,
        int startY,
        int z,
        int maxRadius)
    {
        var visited = new HashSet<(int X, int Y)>();
        var queue = new Queue<(int X, int Y, int Distance)>();

        void Enqueue(int x, int y, int distance)
        {
            if (!world.IsValidPosition(x, y, z)) return;
            if (visited.Add((x, y)))
                queue.Enqueue((x, y, distance));
        }

        foreach (var (dx, dy) in new (int X, int Y)[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            Enqueue(startX + dx, startY + dy, 1);

        foreach (var (dx, dy) in new (int X, int Y)[] { (2, 0), (-2, 0), (0, 2), (0, -2), (1, 1), (1, -1), (-1, 1), (-1, -1) })
            Enqueue(startX + dx, startY + dy, 2);

        while (queue.Count > 0)
        {
            var (x, y, distance) = queue.Dequeue();
            if (distance > maxRadius) break;
            if (!IsStandableOrWalkable(world, x, y, z)) continue;
            if (IsConstructionSiteCell(world, x, y, z)) continue;
            return new WorldPoint3(x, y, z);
        }

        return null;
    }

    public static bool IsStandableOrWalkable(World world, int x, int y, int z)
    {
        var tile = world.GetTile(x, y, z);
        return tile != null && (tile.Value.IsStandable || tile.Value.IsWalkable);
    }

    public static bool IsConstructionSiteCell(World world, int x, int y, int z)
    {
        int cx = x / Chunk.SIZE_XY;
        int cy = y / Chunk.SIZE_XY;
        int lx = x % Chunk.SIZE_XY;
        int ly = y % Chunk.SIZE_XY;
        var chunk = world.GetChunk(new ChunkKey(cx, cy, z));
        var placeables = chunk?.GetPlaceableData();
        if (placeables == null) return false;
        if (!placeables.TryGetOwnedAt(Chunk.LocalIndex(lx, ly), out var owned)) return false;
        return owned.ConstructionSite != null;
    }
}
