using HumanFortress.Simulation.Diagnostics;

namespace HumanFortress.Simulation.Orders;

internal sealed partial class MiningSystem
{
    private static void Log(string message)
    {
        SimulationDiagnostics.Information(LogCallback, "Jobs.Mining", message);
    }

    private bool HasStandableAdjacency(int x, int y, int z)
    {
        bool Acceptable(int tx, int ty)
        {
            var tile = _world.GetTile(tx, ty, z);
            return tile != null && tile.Value.IsWalkable;
        }

        if (Acceptable(x, y - 1) || Acceptable(x + 1, y) || Acceptable(x, y + 1) || Acceptable(x - 1, y))
            return true;

        if (Acceptable(x + 1, y - 1) || Acceptable(x + 1, y + 1) || Acceptable(x - 1, y + 1) || Acceptable(x - 1, y - 1))
            return true;

        for (int radius = 2; radius <= 3; radius++)
        {
            for (int yy = y - radius; yy <= y + radius; yy++)
            {
                int leftX = x - radius;
                int rightX = x + radius;
                if (Acceptable(leftX, yy) || Acceptable(rightX, yy))
                    return true;
            }

            for (int xx = x - radius + 1; xx <= x + radius - 1; xx++)
            {
                int topY = y - radius;
                int bottomY = y + radius;
                if (Acceptable(xx, topY) || Acceptable(xx, bottomY))
                    return true;
            }
        }

        return false;
    }

    private static ulong SeedFrom(int x, int y, int z)
    {
        unchecked
        {
            uint seed = 2166136261;
            seed = (seed ^ (uint)x) * 16777619;
            seed = (seed ^ (uint)y) * 16777619;
            seed = (seed ^ (uint)z) * 16777619;
            return seed;
        }
    }
}
