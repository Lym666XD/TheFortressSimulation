using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class DebugMenuSnapshotBuilder
{
    internal static SimulationDebugMenuData Build(World? world)
    {
        if (world == null)
        {
            return new SimulationDebugMenuData(
                new DebugWorldStatusView(false, 0, 0, 0, 0, 0),
                Array.Empty<DebugItemCategoryView>());
        }

        var itemDefinitions = world.Items.GetAllDefinitions().ToList();
        var status = new DebugWorldStatusView(
            HasWorld: true,
            ChunksLoaded: world.GetAllChunks().Count(),
            ItemInstances: world.Items.InstanceCount,
            ItemDefinitions: world.Items.DefinitionCount,
            CreatureInstances: world.Creatures.GetAllInstances().Count(),
            CreatureDefinitions: world.Creatures.DefinitionCount);

        return new SimulationDebugMenuData(
            status,
            CreateItemCategories(itemDefinitions));
    }

    internal static SimulationDebugSpawnData BuildSpawnData(World? world)
    {
        if (world == null)
            return SimulationDebugSpawnData.Empty;

        return new SimulationDebugSpawnData(
            HasWorld: true,
            ItemDefinitions: world.Items.DefinitionCount,
            CreatureDefinitions: world.Creatures.DefinitionCount);
    }
}
