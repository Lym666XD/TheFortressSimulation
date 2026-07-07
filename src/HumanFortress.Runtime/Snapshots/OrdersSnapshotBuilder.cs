using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static class OrdersSnapshotBuilder
{
    internal static SimulationOrdersDebugData Build(World? world)
    {
        if (world == null)
            return new SimulationOrdersDebugData(0, 0, 0, 0, Array.Empty<RecentDesignationView>());

        var activeHauls = world.Orders.GetActiveHaulsSnapshot();
        var activeMining = world.Orders.GetActiveMiningSnapshot();
        var activeConstruction = world.Orders.GetActiveConstructionSnapshot();
        var activeBuildable = world.Orders.GetActiveBuildableSnapshot();

        var recent = new List<RecentDesignationView>();
        recent.AddRange(world.Orders.GetRecentHauls()
            .Take(2)
            .Select(designation => new RecentDesignationView(
                "Haul",
                $"Rect ({designation.WorldRect.X},{designation.WorldRect.Y}) {designation.WorldRect.Width}x{designation.WorldRect.Height} z={designation.Z}")));
        recent.AddRange(world.Orders.GetRecentMining()
            .Take(2)
            .Select(designation => new RecentDesignationView(
                "Mine",
                $"Rect ({designation.Rect.X},{designation.Rect.Y}) {designation.Rect.Width}x{designation.Rect.Height} z={designation.ZMin}->{designation.ZMax}")));

        return new SimulationOrdersDebugData(
            activeHauls.Count,
            activeMining.Count,
            activeConstruction.Count,
            activeBuildable.Count,
            recent);
    }
}
