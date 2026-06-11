using HumanFortress.Simulation.Orders;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningAdjacencyFinder
{
    private readonly WorldModel _world;

    public MiningAdjacencyFinder(WorldModel world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public (int X, int Y)? FindForAction(MiningAction action, int x, int y, int z)
    {
        static IEnumerable<(int dx, int dy)> Ortho()
        {
            yield return (0, -1);
            yield return (1, 0);
            yield return (0, 1);
            yield return (-1, 0);
        }

        static IEnumerable<(int dx, int dy)> Diag()
        {
            yield return (1, -1);
            yield return (1, 1);
            yield return (-1, 1);
            yield return (-1, -1);
        }

        if (action == MiningAction.DigStairwell || action == MiningAction.DigChannel)
        {
            var self = _world.GetTile(x, y, z);
            if (self != null && self.Value.IsStandable)
            {
                return (x, y);
            }
        }

        bool Acceptable(int tx, int ty)
        {
            var tile = _world.GetTile(tx, ty, z);
            return tile != null && tile.Value.IsWalkable;
        }

        foreach (var (dx, dy) in Ortho())
        {
            if (Acceptable(x + dx, y + dy))
            {
                return (x + dx, y + dy);
            }
        }

        if (action != MiningAction.DigChannel)
        {
            foreach (var (dx, dy) in Diag())
            {
                if (Acceptable(x + dx, y + dy))
                {
                    return (x + dx, y + dy);
                }
            }
        }

        int maxRadius = action == MiningAction.DigStairwell ? 8 : 3;
        for (int r = 2; r <= maxRadius; r++)
        {
            for (int yy = y - r; yy <= y + r; yy++)
            {
                int xx1 = x - r;
                int xx2 = x + r;
                if (Acceptable(xx1, yy))
                {
                    return (xx1, yy);
                }

                if (Acceptable(xx2, yy))
                {
                    return (xx2, yy);
                }
            }

            for (int xx = x - r + 1; xx <= x + r - 1; xx++)
            {
                int yy1 = y - r;
                int yy2 = y + r;
                if (Acceptable(xx, yy1))
                {
                    return (xx, yy1);
                }

                if (Acceptable(xx, yy2))
                {
                    return (xx, yy2);
                }
            }
        }

        return null;
    }
}
