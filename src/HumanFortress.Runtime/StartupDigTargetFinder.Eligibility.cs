using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal static partial class StartupDigTargetFinder
{
    private static bool TryCreateDigTarget(
        World world,
        int x,
        int y,
        int z,
        int tiles,
        out StartupDigTarget target)
    {
        if (x < 0 || y < 0 || x >= tiles || y >= tiles || !IsDiggable(world, x, y, z))
        {
            target = default;
            return false;
        }

        target = new StartupDigTarget(x, y, z);
        return true;
    }

    private static bool IsDiggable(World world, int x, int y, int z)
    {
        var tile = world.GetTile(x, y, z);
        if (tile == null)
            return false;

        var kind = tile.Value.Kind;
        return kind == TerrainKind.SolidWall || kind == TerrainKind.Ramp;
    }
}
