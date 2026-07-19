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
        _afterPostTickCommit?.Invoke(tick);
    }

    private void ApplyCommittedDiffs(ulong tick)
    {
        _mutationCommit.Commit(tick);
    }

    private void RebuildDirtyNavigationChunks()
    {
        // Rebuild navigation for dirty chunks after terrain changes.
        var dirtyChunks = _world.GetAndClearDirtyChunks();
        if (dirtyChunks.Count == 0)
            return;

        Interlocked.Add(ref _dirtyChunksProcessed, dirtyChunks.Count);

        foreach (var ck in dirtyChunks)
        {
            var navigationChunk = new HumanFortress.Contracts.Navigation.ChunkKey(ck.ChunkX, ck.ChunkY, ck.Z);
            if (_navigation != null)
            {
                _navigation.RebuildChunkNavData(navigationChunk);
                Interlocked.Increment(ref _navigationChunkRebuilds);
            }
            _pathServices?.InvalidateChunk(navigationChunk);
        }
    }
}
