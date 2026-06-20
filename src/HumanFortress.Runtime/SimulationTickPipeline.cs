using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

/// <summary>
/// Owns the pre/post tick barriers for one active simulation session.
/// </summary>
public sealed class SimulationTickPipeline
{
    private readonly World _world;
    private readonly SimulationCommandStage _commandStage;
    private readonly DiffLog _diffLog;
    private readonly ItemsDiffLog _itemsDiffLog;
    private readonly CreaturesDiffLog _creaturesDiffLog;
    private readonly NavigationManager? _navigation;
    private readonly IRuntimeGeologyCatalog? _geology;

    public SimulationTickPipeline(
        World world,
        CommandQueue commandQueue,
        IRuntimeCommandContext context,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        CreaturesDiffLog creaturesDiffLog,
        NavigationManager? navigation,
        IRuntimeGeologyCatalog? geology = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _commandStage = new SimulationCommandStage(commandQueue, context);
        _diffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        _itemsDiffLog = itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog));
        _creaturesDiffLog = creaturesDiffLog ?? throw new ArgumentNullException(nameof(creaturesDiffLog));
        _navigation = navigation;
        _geology = geology;
    }

    public void AttachTo(TickScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        scheduler.PreTick += ExecutePreTick;
        scheduler.PostTick += ExecutePostTick;
    }

    public void DetachFrom(TickScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        scheduler.PreTick -= ExecutePreTick;
        scheduler.PostTick -= ExecutePostTick;
    }

    private void ExecutePreTick(ulong tick)
    {
        _commandStage.Execute(tick);
    }

    private void ExecutePostTick(ulong tick)
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

        // Rebuild navigation for dirty chunks after terrain changes.
        var dirtyChunks = _world.GetAndClearDirtyChunks();
        if (dirtyChunks.Count == 0)
            return;

        foreach (var ck in dirtyChunks)
        {
            _navigation?.RebuildChunkNavData(new HumanFortress.Navigation.ChunkKey(ck.ChunkX, ck.ChunkY, ck.Z));
        }
    }
}
