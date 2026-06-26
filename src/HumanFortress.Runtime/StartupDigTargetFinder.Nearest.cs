using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal static partial class StartupDigTargetFinder
{
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
                    if (TryCreateDigTarget(world, cx + dx, cy - dy1, z, tiles, out target))
                        return true;

                    if (dy1 != 0 && TryCreateDigTarget(world, cx + dx, cy + dy1, z, tiles, out target))
                        return true;
                }
            }
        }

        target = default;
        return false;
    }
}
