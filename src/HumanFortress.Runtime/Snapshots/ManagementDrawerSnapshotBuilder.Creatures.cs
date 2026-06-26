using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class ManagementDrawerSnapshotBuilder
{
    private static CreatureDrawerData BuildCreatures(World world)
    {
        var rows = world.Creatures.GetAllInstances()
            .OrderBy(creature => creature.Guid)
            .Select(creature =>
            {
                var definition = world.Creatures.GetDefinition(creature.DefinitionId);
                string name = definition?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    name = creature.DefinitionId;

                return new CreatureDrawerRowView(
                    creature.Guid,
                    name,
                    creature.FactionId,
                    creature.HP > 0,
                    creature.HP,
                    creature.MaxHP,
                    creature.Position.X,
                    creature.Position.Y,
                    creature.Z);
            })
            .ToList();

        int alive = rows.Count(row => row.Alive);
        return new CreatureDrawerData(rows, rows.Count, alive, rows.Count - alive);
    }
}
