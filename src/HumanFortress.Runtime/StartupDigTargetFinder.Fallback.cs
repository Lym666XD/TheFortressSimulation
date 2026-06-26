using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal static partial class StartupDigTargetFinder
{
    private static bool TryFindFirstDigTarget(World world, out StartupDigTarget target)
    {
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
}
