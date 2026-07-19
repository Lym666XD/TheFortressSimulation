using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Diff;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Runtime.Diagnostics;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Host;

/// <summary>
/// Owns the pre/post tick barriers for one active simulation session.
/// </summary>
internal sealed partial class SimulationTickPipeline
{
    private readonly World _world;
    private readonly SimulationCommandStage _commandStage;
    private readonly DiffLog _diffLog;
    private readonly RuntimeMutationDiffLogs _mutationDiffs;
    private readonly IConstructionCatalog _constructions;
    private readonly NavigationManager? _navigation;
    private readonly IRuntimeGeologyCatalog? _geology;
    private readonly RuntimePathServiceRegistry? _pathServices;
    private readonly Action<ulong>? _afterPostTickCommit;
    private readonly TickMutationCommitTransaction _mutationCommit;
    private long _dirtyChunksProcessed;
    private long _navigationChunkRebuilds;

    internal SimulationTickPipeline(
        World world,
        CommandQueue commandQueue,
        IRuntimeCommandClockContext clockContext,
        ISimulationContext commandContext,
        DiffLog diffLog,
        RuntimeMutationDiffLogs mutationDiffs,
        IConstructionCatalog constructions,
        NavigationManager? navigation,
        IRuntimeGeologyCatalog? geology = null,
        RuntimePathServiceRegistry? pathServices = null,
        Action<ulong>? afterPostTickCommit = null,
        Action<TickMutationCommitStage>? afterMutationCommitStage = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _commandStage = new SimulationCommandStage(commandQueue, clockContext, commandContext);
        _diffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        _mutationDiffs = mutationDiffs ?? throw new ArgumentNullException(nameof(mutationDiffs));
        _constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
        _navigation = navigation;
        _geology = geology;
        _pathServices = pathServices;
        _afterPostTickCommit = afterPostTickCommit;
        _mutationCommit = new TickMutationCommitTransaction(
            _world,
            _diffLog,
            _mutationDiffs,
            _constructions,
            _geology,
            afterMutationCommitStage);
    }

    internal SimulationTickPipeline(
        World world,
        CommandQueue commandQueue,
        IRuntimeCommandClockContext clockContext,
        ISimulationContext commandContext,
        DiffLog diffLog,
        RuntimeMutationDiffLogs mutationDiffs,
        NavigationManager? navigation,
        IRuntimeGeologyCatalog? geology = null,
        RuntimePathServiceRegistry? pathServices = null,
        Action<ulong>? afterPostTickCommit = null,
        Action<TickMutationCommitStage>? afterMutationCommitStage = null)
        : this(
            world,
            commandQueue,
            clockContext,
            commandContext,
            diffLog,
            mutationDiffs,
            ConstructionCatalogStore.Empty,
            navigation,
            geology,
            pathServices,
            afterPostTickCommit,
            afterMutationCommitStage)
    {
    }

    internal void AttachTo(TickScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        scheduler.PreTick += ExecutePreTick;
        scheduler.PostTick += ExecutePostTick;
    }

    internal void DetachFrom(TickScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        scheduler.PreTick -= ExecutePreTick;
        scheduler.PostTick -= ExecutePostTick;
    }

    private void ExecutePreTick(ulong tick)
    {
        _commandStage.Execute(tick);
    }

    internal RuntimeTopologyMetricsSnapshot CaptureTopologyMetrics()
    {
        return new RuntimeTopologyMetricsSnapshot(
            Interlocked.Read(ref _dirtyChunksProcessed),
            Interlocked.Read(ref _navigationChunkRebuilds));
    }
}
