using System;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Transport;
using HumanFortress.Navigation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.App.Jobs;

/// <summary>
/// App-owned composition shell for the Jobs-owned transport executor.
/// </summary>
public sealed class TransportJobSystem : ITick
{
    private readonly NavigationManager _nav;
    private readonly TransportJobExecutor _executor;

    public TransportJobSystem(
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
        IPathService? pathService = null)
    {
        _nav = sharedNav ?? SimulationNavigationFactory.Create(world, rebuildAll: true);
        var paths = pathService ?? new PathService(NavigationTuning.LoadFromContent());
        var navView = new WorldNavigationView(_nav);
        var logger = AppTransportJobLogger.Instance;
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
            diffEmitter,
            diffEmitter,
            workerCandidates,
            completionSink,
            logger,
            intakeBudget,
            carryoverMaxTicks,
            maxActiveJobs);
    }

    public int LastIntakeCount => _executor.LastIntakeCount;

    public int Priority => UpdateOrder.Priority.Jobs;

    public string SystemId => TransportJobExecutor.SystemId;

    public NavigationManager NavigationManager => _nav;

    public TransportJobStatsSnapshot GetLastStatsSnapshot() => _executor.GetLastStatsSnapshot();

    public int GetBacklogCount() => _executor.GetBacklogCount();

    public void ReadTick(ulong tick) => _executor.ReadTick(tick);

    public void WriteTick(ulong tick) => _executor.WriteTick(tick);

    public List<TransportActiveJobView> GetActiveJobsSnapshot() => _executor.GetActiveJobsSnapshot();

    public TransportDebugSnapshot GetDebugSnapshot(int maxActive = 8, int maxRequests = 8, bool includeSeeds = false)
        => _executor.GetDebugSnapshot(maxActive, maxRequests, includeSeeds);

    public void ApplySchedulingHints(int? intakeCap, int? maxActiveCap, int reserveSlots)
        => _executor.ApplySchedulingHints(intakeCap, maxActiveCap, reserveSlots);
}
