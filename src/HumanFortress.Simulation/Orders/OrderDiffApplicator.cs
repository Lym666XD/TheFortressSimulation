using HumanFortress.Simulation.Diagnostics;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Orders;

internal static class OrderDiffApplicator
{
    internal static void ApplyAll(SimulationWorld world, IReadOnlyList<OrderDiff> diffs)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diffs);

        if (diffs.Count == 0)
            return;

        foreach (var diff in diffs.OrderBy(static d => d.GetSortKey()))
        {
            try
            {
                Apply(world, diff);
            }
            catch (Exception ex)
            {
                SimulationDiagnostics.Error(
                    world.Diagnostics,
                    "Simulation.OrderDiff",
                    $"[OrderDiffApplicator] Failed to apply diff {diff.Op}: {ex.Message}",
                    ex);
                throw;
            }
        }
    }

    private static void Apply(SimulationWorld world, OrderDiff diff)
    {
        switch (diff.Op)
        {
            case OrderDiffOp.Mining:
                world.Orders.EnqueueMining(diff.WorldRect, diff.Z, diff.Priority, diff.CreatedTick);
                break;

            case OrderDiffOp.AdvancedMining:
                world.Orders.EnqueueMiningAdvanced(
                    diff.WorldRect,
                    diff.ZMin,
                    diff.ZMax,
                    diff.MiningAction,
                    diff.Priority,
                    diff.CreatedTick);
                break;

            case OrderDiffOp.Haul:
                world.Orders.EnqueueHaul(diff.WorldRect, diff.Z, diff.Priority, diff.CreatedTick);
                break;

            case OrderDiffOp.Construction:
                if (diff.MaterialFilter != null)
                {
                    world.Orders.EnqueueConstruction(
                        diff.WorldRect,
                        diff.ZMin,
                        diff.ZMax,
                        diff.ConstructionShape,
                        diff.MaterialFilter,
                        diff.Priority,
                        diff.CreatedTick);
                }
                break;

            case OrderDiffOp.BuildableConstruction:
                world.Orders.EnqueueBuildableConstruction(
                    diff.ConstructionId,
                    diff.Anchor,
                    diff.Z,
                    diff.Priority,
                    diff.CreatedTick);
                break;
        }
    }
}
