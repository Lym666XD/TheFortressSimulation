using HumanFortress.App.Commands;
using HumanFortress.App.UI;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal static class FortressAutoDigBootstrapper
{
    public static void EnqueueAfterWorldFill(World world, FortressRuntimeAccess runtime, int currentZ)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(runtime);

        try
        {
            int tiles = world.SizeInTiles;
            int cx = tiles / 2;
            int cy = tiles / 2;
            int zMin = 0;
            int zMax = Math.Max(0, world.MaxZ - 1);

            if (TryFindDigTarget(world, cx, cy, tiles, zMin, zMax, out var target))
            {
                var rect = new Rectangle(target.X, target.Y, 1, 1);
                Logger.Log($"[DEBUG] Creating mining order command zMin={target.Z} zMax={target.Z} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
                runtime.EnqueueCurrentTickCommand(tick => new CreateAdvancedMiningOrderCommand(
                    tick,
                    rect,
                    target.Z,
                    target.Z,
                    MiningAction.Dig,
                    priority: 50));
                Logger.Log($"[AUTO-DIG] Enqueued test Dig at ({target.X},{target.Y},{target.Z}) after world fill");
                return;
            }

            int fallbackZ = Math.Max(0, Math.Min(world.MaxZ - 1, currentZ));
            var fallbackRect = new Rectangle(cx, cy, 1, 1);
            Logger.Log($"[DEBUG] Creating mining order command zMin={fallbackZ} zMax={fallbackZ} rect=({fallbackRect.X},{fallbackRect.Y},{fallbackRect.Width}x{fallbackRect.Height})");
            runtime.EnqueueCurrentTickCommand(tick => new CreateAdvancedMiningOrderCommand(
                tick,
                fallbackRect,
                fallbackZ,
                fallbackZ,
                MiningAction.Dig,
                priority: 50));
            Logger.Log($"[AUTO-DIG] Enqueued fallback Dig at ({fallbackRect.X},{fallbackRect.Y},{fallbackZ})");
        }
        catch (Exception ex)
        {
            Logger.Log($"[AUTO-DIG] ERROR (UI phase): {ex.Message}");
        }
    }

    private static bool TryFindDigTarget(World world, int cx, int cy, int tiles, int zMin, int zMax, out (int X, int Y, int Z) target)
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
                            target = (x, y, z);
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
