using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class MapViewportSnapshotBuilder
{
    private static void AddEntityCells(
        List<MapViewportCellView> cells,
        World world,
        Point cameraPosition,
        int currentZ,
        int viewWidth,
        int viewHeight)
    {
        var creatures = world.Creatures.GetAllInstances()
            .Where(creature => creature.Z == currentZ && creature.HP > 0)
            .ToList();
        var creaturePositions = new HashSet<Point>();

        foreach (var creature in creatures)
        {
            creaturePositions.Add(creature.Position);

            int screenX = creature.Position.X - cameraPosition.X;
            int screenY = creature.Position.Y - cameraPosition.Y;
            if (!IsOnScreen(screenX, screenY, viewWidth, viewHeight))
                continue;

            var (glyph, color) = GetCreatureDisplay(world, creature);
            cells.Add(new MapViewportCellView(screenX, screenY, glyph, color.ToSnapshotColor()));
        }

        foreach (var item in world.Items.GetGroundInstancesAtZ(currentZ))
        {
            int screenX = item.Position.X - cameraPosition.X;
            int screenY = item.Position.Y - cameraPosition.Y;
            if (!IsOnScreen(screenX, screenY, viewWidth, viewHeight))
                continue;

            if (creaturePositions.Contains(item.Position))
                continue;

            var (glyph, color) = GetItemDisplay(world, item);
            cells.Add(new MapViewportCellView(screenX, screenY, glyph, color.ToSnapshotColor()));
        }
    }

}
