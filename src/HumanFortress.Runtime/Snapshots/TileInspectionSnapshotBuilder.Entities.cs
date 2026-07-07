using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class TileInspectionSnapshotBuilder
{
    private static IReadOnlyList<TileInspectionItemView> BuildItemViews(World world, Point tileWorldPosition, int tileZ)
    {
        return world.Items.GetGroundItemsAt(tileWorldPosition, tileZ)
            .Select(item =>
            {
                var definition = world.Items.GetDefinition(item.DefinitionId);
                return new TileInspectionItemView(definition?.Name ?? item.DefinitionId, item.StackCount);
            })
            .ToList();
    }

    private static IReadOnlyList<TileInspectionCreatureView> BuildCreatureViews(World world, Point tileWorldPosition, int tileZ)
    {
        return world.Creatures.GetAllInstances()
            .Where(creature => creature.Position.X == tileWorldPosition.X
                && creature.Position.Y == tileWorldPosition.Y
                && creature.Z == tileZ)
            .Select(creature =>
            {
                var definition = world.Creatures.GetDefinition(creature.DefinitionId);
                return new TileInspectionCreatureView(
                    definition?.Name ?? creature.DefinitionId,
                    creature.HP,
                    creature.MaxHP);
            })
            .ToList();
    }
}
