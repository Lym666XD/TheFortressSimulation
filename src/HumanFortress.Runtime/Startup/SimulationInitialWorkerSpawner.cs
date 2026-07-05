using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Startup;

/// <summary>
/// Seeds a new simulation session with basic workers when no loaded creatures exist.
/// </summary>
internal static class SimulationInitialWorkerSpawner
{
    internal static int SpawnIfNeeded(World world, int desired = 5, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Creatures.InstanceCount > 0) return 0;

        try
        {
            int tiles = world.SizeInTiles;
            int cx = tiles / 2;
            int cy = tiles / 2;

            int zMid = Math.Max(0, Math.Min(world.MaxZ - 1, world.MaxZ / 2));
            int zMin = Math.Max(0, zMid - 5);
            int zMax = Math.Min(world.MaxZ - 1, zMid + 5);

            int spawned = 0;
            int radiusMax = Math.Max(4, tiles / 8);
            for (int radius = 0; radius <= radiusMax && spawned < desired; radius++)
            {
                for (int z = zMin; z <= zMax && spawned < desired; z++)
                {
                    for (int dx = -radius; dx <= radius && spawned < desired; dx++)
                    {
                        int dy1 = radius - Math.Abs(dx);
                        foreach (int dy in new[] { -dy1, dy1 })
                        {
                            int wx = cx + dx;
                            int wy = cy + dy;
                            if (wx < 0 || wy < 0 || wx >= tiles || wy >= tiles) continue;

                            var tile = world.GetTile(wx, wy, z);
                            if (tile == null) continue;
                            if (!(tile.Value.IsStandable || tile.Value.IsWalkable)) continue;

                            var guid = world.Creatures.SpawnCreature("core_race_dwarf", new Point(wx, wy), z, "player", 0);
                            if (guid.HasValue)
                            {
                                spawned++;
                                if (spawned >= desired) break;
                            }
                        }
                    }
                }
            }

            if (spawned < desired)
            {
                for (int z = 0; z < world.MaxZ && spawned < desired; z++)
                {
                    for (int wy = 0; wy < tiles && spawned < desired; wy++)
                    {
                        for (int wx = 0; wx < tiles && spawned < desired; wx++)
                        {
                            var tile = world.GetTile(wx, wy, z);
                            if (tile == null || !(tile.Value.IsStandable || tile.Value.IsWalkable)) continue;

                            var guid = world.Creatures.SpawnCreature("core_race_dwarf", new Point(wx, wy), z, "player", 0);
                            if (guid.HasValue) spawned++;
                        }
                    }
                }
            }

            log?.Invoke($"[SIM] Initial workers spawned: {spawned}");
            return spawned;
        }
        catch (Exception ex)
        {
            log?.Invoke($"[SIM] Spawn initial workers failed: {ex.Message}");
            return 0;
        }
    }
}
