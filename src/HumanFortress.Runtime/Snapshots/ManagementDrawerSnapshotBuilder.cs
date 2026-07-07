using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class ManagementDrawerSnapshotBuilder
{
    internal static SimulationManagementDrawerData Build(World? world)
    {
        if (world == null)
            return SimulationManagementDrawerData.Empty;

        return new SimulationManagementDrawerData(
            true,
            BuildCreatures(world),
            BuildItems(world),
            BuildZones(world),
            BuildStockpiles(world));
    }
}
