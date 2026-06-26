using HumanFortress.Contracts.Navigation;
using System;
using HumanFortress.Jobs;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Transport;
using HumanFortress.Navigation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Runtime.Jobs;

/// <summary>
/// Tick-facing composition shell for the Jobs-owned transport executor.
/// </summary>
internal sealed class TransportJobSystem : ITick, IUnifiedTransportJobExecutor
{
    private readonly NavigationManager _nav;
    private readonly TransportJobExecutor _executor;

    internal TransportJobSystem(
        HumanFortress.Simulation.World.World world,
        ITransportRequestQueue requestQueue,
        DiffLog? diffLog = null,
        NavigationManager? sharedNav = null,
        ItemsDiffLog? itemsDiffLog = null,
        int intakeBudget = 16,
        int carryoverMaxTicks = 8,
        int maxActiveJobs = 0,
        ProfessionAssignments? professions = null,
        WorkerSelectionStrategy workerStrategy = WorkerSelectionStrategy.Closest,
        IPathService? pathService = null,
        NavigationTuning? navigationTuning = null,
        Action<string>? log = null)
    {
        var tuning = navigationTuning ?? NavigationTuning.Default;
        _nav = sharedNav ?? SimulationNavigationFactory.Create(world, rebuildAll: true, tuning);
        var paths = pathService ?? new PathService(tuning);
        var navView = new WorldNavigationView(_nav);
        var move = new MovementExecutor(paths);
        var logger = new TransportCallbackJobLogger(log);
        ITransportWorkerCandidateSource? workerCandidates = professions == null
            ? null
            : new TransportProfessionCandidateSource(professions, workerStrategy);
        ITransportJobCompletionSink? completionSink = professions == null
            ? null
            : new TransportProfessionCompletionSink(professions, "hauling");
        var diffEmitter = new TransportDiffEmitter(
            diffLog,
            itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog), "TransportJobSystem requires ItemsDiffLog for deterministic split-stack hauling."),
            UpdateOrder.Priority.Jobs,
            TransportJobExecutor.SystemId);

        _executor = new TransportJobExecutor(
            world,
            requestQueue,
            paths,
            navView,
            move,
            diffEmitter,
            diffEmitter,
            workerCandidates,
            completionSink,
            logger,
            intakeBudget,
            carryoverMaxTicks,
            maxActiveJobs);
    }

    internal int LastIntakeCount => _executor.LastIntakeCount;

    internal int Priority => UpdateOrder.Priority.Jobs;

    internal string SystemId => TransportJobExecutor.SystemId;

    internal NavigationManager NavigationManager => _nav;

    int IUnifiedJobExecutor.LastIntakeCount => LastIntakeCount;

    int ITick.Priority => Priority;

    string ITick.SystemId => SystemId;

    void ITick.ReadTick(ulong tick) => ReadTick(tick);

    void ITick.WriteTick(ulong tick) => WriteTick(tick);

    internal void ReadTick(ulong tick) => _executor.ReadTick(tick);

    internal void WriteTick(ulong tick) => _executor.WriteTick(tick);

    internal TransportJobStatsSnapshot GetLastStatsSnapshot() => _executor.GetLastStatsSnapshot();

    TransportJobStatsSnapshot IUnifiedTransportJobExecutor.GetLastStatsSnapshot() => GetLastStatsSnapshot();

    internal int GetBacklogCount() => _executor.GetBacklogCount();

    internal List<TransportActiveJobView> GetActiveJobsSnapshot() => _executor.GetActiveJobsSnapshot();

    internal TransportDebugSnapshot GetDebugSnapshot(int maxActive = 8, int maxRequests = 8, bool includeSeeds = false)
        => _executor.GetDebugSnapshot(maxActive, maxRequests, includeSeeds);

    internal void ApplySchedulingHints(int? intakeCap, int? maxActiveCap, int reserveSlots)
        => _executor.ApplySchedulingHints(intakeCap, maxActiveCap, reserveSlots);

    void IUnifiedTransportJobExecutor.ApplySchedulingHints(int? intakeCap, int? maxActiveCap, int reserveSlots)
        => ApplySchedulingHints(intakeCap, maxActiveCap, reserveSlots);
}
