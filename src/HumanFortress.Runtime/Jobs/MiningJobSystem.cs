using System;
using HumanFortress.Jobs;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Mining;
using HumanFortress.Navigation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Jobs;

/// <summary>
/// Tick-facing composition shell for the Jobs-owned mining executor.
/// </summary>
internal sealed class MiningJobSystem : ITick, IUnifiedMiningJobExecutor
{
    private readonly NavigationManager _nav;
    private readonly MiningJobExecutor _executor;

    internal MiningJobSystem(
        HumanFortress.Simulation.World.World world,
        MiningSystem planner,
        DiffLog? diffLog = null,
        HumanFortress.Simulation.Items.ItemsDiffLog? itemsDiff = null,
        NavigationManager? sharedNav = null,
        int intakeBudget = 16,
        int carryoverMaxTicks = 8,
        ProfessionAssignments? professions = null,
        WorkerSelectionStrategy workerStrategy = WorkerSelectionStrategy.Closest,
        NavigationTuning? navigationTuning = null,
        string? miningTuningJson = null,
        IRuntimeGeologyCatalog? geology = null,
        Action<string>? log = null)
    {
        var tuning = navigationTuning ?? NavigationTuning.Default;
        _nav = sharedNav ?? SimulationNavigationFactory.Create(world, rebuildAll: true, tuning);
        var paths = new PathService(tuning);
        var navView = new WorldNavigationView(_nav);
        var move = new MovementExecutor(paths);
        var logger = new MiningCallbackJobLogger(log);
        var dropResolver = new MiningDropResolver(geology, miningTuningJson, log);
        var diffEmitter = new MiningDiffEmitter(diffLog, itemsDiff, SystemId, Priority);
        IMiningWorkerCandidateSource? workerCandidates = professions == null
            ? null
            : new MiningProfessionCandidateSource(professions, workerStrategy);
        IMiningJobCompletionSink? completionSink = professions == null
            ? null
            : new MiningProfessionCompletionSink(professions, "mining");

        _executor = new MiningJobExecutor(
            world,
            planner,
            paths,
            navView,
            diffEmitter,
            dropResolver,
            workerCandidates,
            completionSink,
            move,
            logger,
            intakeBudget,
            carryoverMaxTicks);
    }

    internal int LastIntakeCount => _executor.LastIntakeCount;

    internal int Priority => UpdateOrder.Priority.Jobs;

    internal string SystemId => MiningJobExecutor.SystemId;

    internal NavigationManager NavigationManager => _nav;

    int IUnifiedJobExecutor.LastIntakeCount => LastIntakeCount;

    int ITick.Priority => Priority;

    string ITick.SystemId => SystemId;

    void ITick.ReadTick(ulong tick) => ReadTick(tick);

    void ITick.WriteTick(ulong tick) => WriteTick(tick);

    internal void ReadTick(ulong tick) => _executor.ReadTick(tick);

    internal void WriteTick(ulong tick) => _executor.WriteTick(tick);

    internal List<(Point Cell, int Z)> GetRecentCompletions(ulong now) => _executor.GetRecentCompletions(now);

    internal List<MiningActiveJobView> GetActiveJobsSnapshot() => _executor.GetActiveJobsSnapshot();

    internal MiningDebugSnapshot GetDebugSnapshot(int maxActive = 8, bool includeSeeds = false)
        => _executor.GetDebugSnapshot(maxActive, includeSeeds);

    internal int GetBacklogCount() => _executor.GetBacklogCount();

    int IUnifiedMiningJobExecutor.GetBacklogCount() => GetBacklogCount();

    internal int GetDeferredCount() => _executor.GetDeferredCount();

    internal int GetReservedTileCount() => _executor.GetReservedTileCount();

    internal MiningJobStatsSnapshot GetLastStatsSnapshot() => _executor.GetLastStatsSnapshot();

    MiningJobStatsSnapshot IUnifiedMiningJobExecutor.GetLastStatsSnapshot() => GetLastStatsSnapshot();
}
