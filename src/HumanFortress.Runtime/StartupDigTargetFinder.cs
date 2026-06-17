using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

public readonly record struct StartupDigTarget(int X, int Y, int Z);

/// <summary>
/// Shared startup helper for optional auto-dig bootstrapping.
/// </summary>
public static class StartupDigTargetFinder
{
    public static bool TryFindNearestDigTarget(World world, out StartupDigTarget target)
    {
        ArgumentNullException.ThrowIfNull(world);

        int tiles = world.SizeInTiles;
        int cx = tiles / 2;
        int cy = tiles / 2;
        int zMin = 0;
        int zMax = Math.Max(0, world.MaxZ - 1);

        return TryFindNearestDigTarget(world, cx, cy, tiles, zMin, zMax, out target);
    }

    public static bool TryFindAnyDigTarget(World world, out StartupDigTarget target)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (TryFindNearestDigTarget(world, out target))
        {
            return true;
        }

        int tiles = world.SizeInTiles;
        for (int z = 0; z < world.MaxZ; z++)
        {
            for (int y = 0; y < tiles; y++)
            {
                for (int x = 0; x < tiles; x++)
                {
                    if (IsDiggable(world, x, y, z))
                    {
                        target = new StartupDigTarget(x, y, z);
                        return true;
                    }
                }
            }
        }

        target = default;
        return false;
    }

    private static bool TryFindNearestDigTarget(
        World world,
        int cx,
        int cy,
        int tiles,
        int zMin,
        int zMax,
        out StartupDigTarget target)
    {
        for (int z = zMin; z <= zMax; z++)
        {
            for (int radius = 0; radius <= Math.Max(cx, cy); radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int dy1 = radius - Math.Abs(dx);
                    foreach (int dy in new[] { -dy1, dy1 })
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        if (x < 0 || y < 0 || x >= tiles || y >= tiles) continue;
                        if (IsDiggable(world, x, y, z))
                        {
                            target = new StartupDigTarget(x, y, z);
                            return true;
                        }
                    }
                }
            }
        }

        target = default;
        return false;
    }

    private static bool IsDiggable(World world, int x, int y, int z)
    {
        var tile = world.GetTile(x, y, z);
        if (tile == null) return false;
        var kind = tile.Value.Kind;
        return kind == TerrainKind.SolidWall || kind == TerrainKind.Ramp;
    }
}
