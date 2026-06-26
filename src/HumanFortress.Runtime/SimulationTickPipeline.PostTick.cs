using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;

namespace HumanFortress.Runtime;

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
        var items = _itemsDiffLog.MergeAndSort();
        ItemsDiffApplicator.ApplyPreSimulation(_world, items);

        var merged = _diffLog.MergeAndSort();
        SimulationDiffApplicator.ApplyAll(_world, merged, _geology);
        _diffLog.Clear();

        var creatureDiffs = _creaturesDiffLog.MergeAndSort();
        CreaturesDiffApplicator.ApplyAll(_world, creatureDiffs, tick);
        _creaturesDiffLog.Clear();

        // Spawn new items after terrain changes, so mining drops see the post-dig tile.
        ItemsDiffApplicator.ApplyAdditions(_world, items, tick);
        _itemsDiffLog.Clear();
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
