using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Shared eligibility and counting rules for mining selections.
/// Centralizes UI and command-side prechecks to avoid drift.
/// </summary>
internal static class MiningOrderRules
{
    internal static int CountEligible(World.World world, Rectangle rect, int zMin, int zMax, MiningAction action)
    {
        int count = 0;
        int z0 = Math.Min(zMin, zMax);
        int z1 = Math.Max(zMin, zMax);
        for (int z = z0; z <= z1; z++)
        {
            for (int y = rect.Y; y <= rect.MaxExtentY; y++)
            for (int x = rect.X; x <= rect.MaxExtentX; x++)
            {
                if (!world.IsValidPosition(x, y, z)) continue;
                var t = world.GetTile(x, y, z);
                if (t == null) continue;
                var k = t.Value.Kind;
                switch (action)
                {
                    case MiningAction.Dig:
                        if (k == TerrainKind.SolidWall || k == TerrainKind.Ramp) count++;
                        break;
                    case MiningAction.DigRamp:
                        if (k == TerrainKind.SolidWall) count++;
                        break;
                    case MiningAction.DigChannel:
                        if (k == TerrainKind.OpenWithFloor) count++;
                        break;
                    case MiningAction.DigStairwell:
                        // Only consider top layer eligibility (as a starting anchor)
                        if (z == z0 && (k == TerrainKind.OpenWithFloor || k == TerrainKind.SolidWall || k == TerrainKind.Ramp)) count++;
                        break;
                    default:
                        if (k == TerrainKind.SolidWall || k == TerrainKind.Ramp) count++;
                        break;
                }
            }
        }
        return count;
    }
}
