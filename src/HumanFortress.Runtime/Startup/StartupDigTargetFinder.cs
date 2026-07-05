using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Startup;

internal readonly record struct StartupDigTarget(int X, int Y, int Z);

/// <summary>
/// Shared startup helper for optional auto-dig bootstrapping.
/// </summary>
internal static partial class StartupDigTargetFinder
{
    internal static bool TryFindNearestDigTarget(World world, out StartupDigTarget target)
    {
        ArgumentNullException.ThrowIfNull(world);

        int tiles = world.SizeInTiles;
        int cx = tiles / 2;
        int cy = tiles / 2;
        int zMin = 0;
        int zMax = Math.Max(0, world.MaxZ - 1);

        return TryFindNearestDigTarget(world, cx, cy, tiles, zMin, zMax, out target);
    }

    internal static bool TryFindAnyDigTarget(World world, out StartupDigTarget target)
    {
        ArgumentNullException.ThrowIfNull(world);

        return TryFindNearestDigTarget(world, out target)
            || TryFindFirstDigTarget(world, out target);
    }
}
