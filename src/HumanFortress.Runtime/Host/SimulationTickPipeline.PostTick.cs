using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.Zones;

namespace HumanFortress.Runtime.Host;

internal sealed partial class SimulationTickPipeline
{
    private void ExecutePostTick(ulong tick)
    {
        ApplyCommittedDiffs(tick);
        RebuildDirtyNavigationChunks();
    }

    private void ApplyCommittedDiffs(ulong tick)
    {
        // Apply item removals/splits before terrain/entity diffs can relocate or carry them.
        var items = _mutationDiffs.Items.MergeAndSort();
        ItemsDiffApplicator.ApplyPreSimulation(_world, items);

        var merged = _diffLog.MergeAndSort();
        SimulationDiffApplicator.ApplyAll(_world, merged, _geology);
        _diffLog.Clear();

        var creatureDiffs = _mutationDiffs.Creatures.MergeAndSort();
        CreaturesDiffApplicator.ApplyAll(_world, creatureDiffs, tick);
        _mutationDiffs.Creatures.Clear();

        // Spawn new items after terrain changes, so mining drops see the post-dig tile.
        ItemsDiffApplicator.ApplyAdditions(_world, items, tick);
        _mutationDiffs.Items.Clear();

        var orderDiffs = _mutationDiffs.Orders.MergeAndSort();
        OrderDiffApplicator.ApplyAll(_world, orderDiffs);
        _mutationDiffs.Orders.Clear();

        var workshopDiffs = _mutationDiffs.Workshops.MergeAndSort();
        WorkshopDiffApplicator.ApplyAll(_world, workshopDiffs, _constructions);
        _mutationDiffs.Workshops.Clear();

        var zoneDiffs = _mutationDiffs.Zones.MergeAndSort();
        ZoneDiffApplicator.ApplyAll(_world, zoneDiffs);
        _mutationDiffs.Zones.Clear();

        var stockpileDiffs = _mutationDiffs.Stockpiles.MergeAndSort();
        StockpileDiffApplicator.ApplyAll(_world, stockpileDiffs);
        _mutationDiffs.Stockpiles.Clear();

        _mutationDiffs.Professions.ApplyAll();
    }

    private void RebuildDirtyNavigationChunks()
    {
        // Rebuild navigation for dirty chunks after terrain changes.
        var dirtyChunks = _world.GetAndClearDirtyChunks();
        if (dirtyChunks.Count == 0)
            return;

        foreach (var ck in dirtyChunks)
            _navigation?.RebuildChunkNavData(new HumanFortress.Contracts.Navigation.ChunkKey(ck.ChunkX, ck.ChunkY, ck.Z));
    }
}
