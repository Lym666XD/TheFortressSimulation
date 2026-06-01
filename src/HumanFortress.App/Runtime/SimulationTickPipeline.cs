using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Owns the pre/post tick barriers for one active simulation session.
/// </summary>
internal sealed class SimulationTickPipeline
{
    private readonly World _world;
    private readonly CommandQueue _commandQueue;
    private readonly SimulationRuntimeContext _context;
    private readonly DiffLog _diffLog;
    private readonly ItemsDiffLog _itemsDiffLog;
    private readonly NavigationManager? _navigation;

    public SimulationTickPipeline(
        World world,
        CommandQueue commandQueue,
        SimulationRuntimeContext context,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager? navigation)
    {
        _world = world;
        _commandQueue = commandQueue;
        _context = context;
        _diffLog = diffLog;
        _itemsDiffLog = itemsDiffLog;
        _navigation = navigation;
    }

    public void AttachTo(TickScheduler scheduler)
    {
        scheduler.PreTick += ExecutePreTick;
        scheduler.PostTick += ExecutePostTick;
    }

    public void DetachFrom(TickScheduler scheduler)
    {
        scheduler.PreTick -= ExecutePreTick;
        scheduler.PostTick -= ExecutePostTick;
    }

    private void ExecutePreTick(ulong tick)
    {
        _context.SetCurrentTick(tick);
        _commandQueue.ExecuteCommands(tick, _context);
    }

    private void ExecutePostTick(ulong tick)
    {
        // Apply item removals/splits before terrain/entity diffs can relocate or carry them.
        var items = _itemsDiffLog.MergeAndSort();
        ItemsDiffApplicator.ApplyPreSimulation(_world, items);

        var merged = _diffLog.MergeAndSort();
        SimulationDiffApplicator.ApplyAll(_world, merged);
        _diffLog.Clear();

        // Spawn new items after terrain changes, so mining drops see the post-dig tile.
        ItemsDiffApplicator.ApplyAdditions(_world, items, tick);
        _itemsDiffLog.Clear();

        // Rebuild navigation for dirty chunks after terrain changes.
        var dirtyChunks = _world.GetAndClearDirtyChunks();
        if (dirtyChunks.Count == 0)
            return;

        foreach (var ck in dirtyChunks)
        {
            var chunk = _world.GetChunk(ck);
            if (chunk != null)
            {
                _navigation?.RebuildChunkNavData(chunk);
            }
        }
    }
}
