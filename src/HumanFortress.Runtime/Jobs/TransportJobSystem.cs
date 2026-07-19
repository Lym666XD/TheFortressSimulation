using HumanFortress.Contracts.Navigation;
using System;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Configuration;
using HumanFortress.Jobs.Diff;
using HumanFortress.Jobs.Logging;
using HumanFortress.Jobs.Orchestration;
using HumanFortress.Jobs.Profession;
using HumanFortress.Jobs.Transport;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Stockpile;

namespace HumanFortress.Runtime.Jobs;

/// <summary>
/// Tick-facing composition shell for the Jobs-owned transport executor.
/// </summary>
internal sealed class TransportJobSystem : IUnifiedTransportJobExecutor, IReadPlanStage
{
    private readonly NavigationManager _nav;
    private readonly TransportJobExecutor _executor;
    private readonly IPathService _paths;
    private readonly int _planningWorkerCount;

    internal TransportJobSystem(
        HumanFortress.Simulation.World.World world,
        ITransportRequestQueue requestQueue,
        DiffLog? diffLog = null,
        NavigationManager? sharedNav = null,
        ItemsDiffLog? itemsDiffLog = null,
        StockpileDiffLog? stockpileDiffLog = null,
        int intakeBudget = 16,
        int carryoverMaxTicks = 8,
        int maxActiveJobs = 0,
        ProfessionAssignments? professions = null,
        WorkerSelectionStrategy workerStrategy = WorkerSelectionStrategy.Closest,
        IPathService? pathService = null,
        NavigationTuning? navigationTuning = null,
        RuntimeNavigationServices? navigationServices = null,
        Action<string>? log = null,
        int planningWorkerCount = 1)
    {
        if (planningWorkerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(planningWorkerCount));

        _planningWorkerCount = planningWorkerCount;
        var tuning = navigationTuning ?? NavigationTuning.Default;
        _nav = sharedNav ?? SimulationNavigationFactory.Create(world, rebuildAll: true, tuning);
        var jobNavigation = (navigationServices ?? new RuntimeNavigationServices(null, tuning)).CreateJobServices(_nav, pathService);
        _paths = jobNavigation.PathService;
        var logger = new TransportCallbackJobLogger(log);
        ITransportWorkerCandidateSource? workerCandidates = professions == null
            ? null
            : new TransportProfessionCandidateSource(professions, workerStrategy);
        TransportProfessionCompletionSink? professionCompletionSink = professions == null
            ? null
            : new TransportProfessionCompletionSink(professions, "hauling");
        ITransportJobCompletionSink? completionSink = professionCompletionSink;
        var diffEmitter = new TransportDiffEmitter(
            diffLog,
            itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog), "TransportJobSystem requires ItemsDiffLog for deterministic split-stack hauling."),
            UpdateOrder.Priority.Jobs,
            TransportJobExecutor.SystemId);
        TransportStockpileIndexEmitter? stockpileIndexEmitter = stockpileDiffLog == null
            ? null
            : new TransportStockpileIndexEmitter(
                world,
                stockpileDiffLog,
                UpdateOrder.Priority.Jobs,
                TransportJobExecutor.SystemId);
        var commitParticipants = new List<ITransportCommitMutationParticipant> { diffEmitter };
        if (stockpileIndexEmitter != null)
            commitParticipants.Add(stockpileIndexEmitter);
        if (professionCompletionSink != null)
            commitParticipants.Add(professionCompletionSink);
        var commitMutations = new TransportCommitMutationCoordinator(commitParticipants.ToArray());

        _executor = new TransportJobExecutor(
            world,
            requestQueue,
            jobNavigation.PathService,
            jobNavigation.WorldView,
            jobNavigation.Movement,
            diffEmitter,
            diffEmitter,
            stockpileIndexEmitter,
            workerCandidates,
            completionSink,
            commitMutations,
            logger,
            intakeBudget,
            carryoverMaxTicks,
            maxActiveJobs);
    }

    internal int LastIntakeCount => _executor.LastIntakeCount;

    internal int PlanningWorkerCountConfigured => _planningWorkerCount;

    internal int Priority => UpdateOrder.Priority.Jobs;

    internal string SystemId => TransportJobExecutor.SystemId;

    internal NavigationManager NavigationManager => _nav;

    internal IPathService PathService => _paths;

    int IUnifiedJobExecutor.LastIntakeCount => LastIntakeCount;

    internal void PrepareSequentialCompatibility(ulong tick) => _executor.PrepareSequentialCompatibility(tick);

    internal void ApplySequentialCompatibility(ulong tick) => _executor.ApplySequentialCompatibility(tick);

    void ISequentialCompatibilityStage.PrepareSequentialCompatibility(ulong tick)
        => PrepareSequentialCompatibility(tick);

    void ISequentialCompatibilityStage.ApplySequentialCompatibility(ulong tick)
        => ApplySequentialCompatibility(tick);

    void IReadPlanStage.ReadPlan(ulong tick) => _executor.ReadPlan(tick, _planningWorkerCount);

    internal void ReadPlan(ulong tick, int workerCount = 1) => _executor.ReadPlan(tick, workerCount);

    internal void SetCommitProbe(Action<TransportCommitStage>? probe) => _executor.SetCommitProbe(probe);

    internal MovementCursorData? GetMovementCursorSnapshot(Guid creatureId) =>
        _executor.GetMovementCursorSnapshot(creatureId);

    internal TransportJobStatsSnapshot GetLastStatsSnapshot() => _executor.GetLastStatsSnapshot();

    TransportJobStatsSnapshot IUnifiedTransportJobExecutor.GetLastStatsSnapshot() => GetLastStatsSnapshot();

    internal int GetBacklogCount() => _executor.GetBacklogCount();

    internal List<TransportActiveJobView> GetActiveJobsSnapshot() => _executor.GetActiveJobsSnapshot();

    internal TransportDebugSnapshot GetDebugSnapshot(int maxActive = 8, int maxRequests = 8, bool includeSeeds = false)
        => _executor.GetDebugSnapshot(maxActive, maxRequests, includeSeeds);

    internal TransportJobReplaySnapshot GetReplaySnapshot() => _executor.GetReplaySnapshot();

    internal TransportJobRestoreResult RestoreReplaySnapshot(
        TransportRequestQueueStateSnapshot queue,
        TransportJobReplaySnapshot executor)
    {
        return _executor.RestoreReplaySnapshot(queue, executor);
    }

    internal void ApplySchedulingHints(int? intakeCap, int? maxActiveCap, int reserveSlots)
        => _executor.ApplySchedulingHints(intakeCap, maxActiveCap, reserveSlots);

    void IUnifiedTransportJobExecutor.ApplySchedulingHints(int? intakeCap, int? maxActiveCap, int reserveSlots)
        => ApplySchedulingHints(intakeCap, maxActiveCap, reserveSlots);
}
