using HumanFortress.Contracts.Runtime;
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
        RuntimeViewportGeometry viewport)
    {
        var creatures = world.Creatures.GetAllInstances()
            .Where(creature => creature.Z == viewport.CurrentZ && creature.HP > 0)
            .ToList();
        var creaturePositions = new HashSet<Point>();

        foreach (var creature in creatures)
        {
            creaturePositions.Add(creature.Position);

            if (!RuntimeViewportGeometryMath.TryWorldToLocal(
                    viewport,
                    new RuntimePoint(creature.Position.X, creature.Position.Y),
                    out var screenPosition))
                continue;

            var (glyph, color) = GetCreatureDisplay(world, creature);
            cells.Add(new MapViewportCellView(screenPosition.X, screenPosition.Y, glyph, color.ToSnapshotColor()));
        }

        foreach (var item in world.Items.GetGroundInstancesAtZ(viewport.CurrentZ))
        {
            if (!RuntimeViewportGeometryMath.TryWorldToLocal(
                    viewport,
                    new RuntimePoint(item.Position.X, item.Position.Y),
                    out var screenPosition))
                continue;

            if (creaturePositions.Contains(item.Position))
                continue;

            var (glyph, color) = GetItemDisplay(world, item);
            cells.Add(new MapViewportCellView(screenPosition.X, screenPosition.Y, glyph, color.ToSnapshotColor()));
        }
    }

}
