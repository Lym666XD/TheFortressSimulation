using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class PlacementPreviewSnapshotBuilder
{
    private static bool IsEligible(World world, int x, int y, int z, SimulationPlacementPreviewMode mode)
    {
        return mode switch
        {
            SimulationPlacementPreviewMode.GroundItems => world.Items.GetGroundItemsAt(new Point(x, y), z).Any(item => !item.Forbidden),
            SimulationPlacementPreviewMode.MiningDig => IsTerrain(world, x, y, z, TerrainKind.SolidWall, TerrainKind.Ramp),
            SimulationPlacementPreviewMode.MiningRamp => IsTerrain(world, x, y, z, TerrainKind.SolidWall),
            SimulationPlacementPreviewMode.MiningChannel => IsTerrain(world, x, y, z, TerrainKind.OpenWithFloor),
            SimulationPlacementPreviewMode.MiningStairwell => world.GetTile(x, y, z).HasValue,
            SimulationPlacementPreviewMode.MiningStairwellTop => IsTerrain(world, x, y, z, TerrainKind.OpenWithFloor),
            SimulationPlacementPreviewMode.ConstructionWall => CanBuildWall(world, x, y, z),
            SimulationPlacementPreviewMode.ConstructionFloor => CanBuildFloor(world, x, y, z),
            SimulationPlacementPreviewMode.ConstructionRamp => CanBuildRamp(world, x, y, z),
            _ => false,
        };
    }

    private static bool IsTerrain(World world, int x, int y, int z, params TerrainKind[] terrainKinds)
    {
        var tile = world.GetTile(x, y, z);
        return tile.HasValue && terrainKinds.Contains(tile.Value.Kind);
    }

    private static bool CanBuildWall(World world, int x, int y, int z)
    {
        var tile = world.GetTile(x, y, z);
        return tile.HasValue && tile.Value.Kind != TerrainKind.SolidWall;
    }

    private static bool CanBuildFloor(World world, int x, int y, int z)
    {
        var tile = world.GetTile(x, y, z);
        if (!tile.HasValue || tile.Value.Kind == TerrainKind.OpenWithFloor)
            return false;

        var below = world.GetTile(x, y, z - 1);
        return below.HasValue && below.Value.ProvidesSupport;
    }

    private static bool CanBuildRamp(World world, int x, int y, int z)
    {
        var top = world.GetTile(x, y, z + 1);
        if (!top.HasValue || top.Value.Kind != TerrainKind.OpenNoFloor)
            return false;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var neighbor = world.GetTile(x + dx, y + dy, z + 1);
                if (neighbor.HasValue && neighbor.Value.IsStandable)
                    return true;
            }
        }

        return false;
    }
}
