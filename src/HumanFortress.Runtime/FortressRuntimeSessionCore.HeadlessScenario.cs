using HumanFortress.Runtime.Diagnostics;
using HumanFortress.Runtime.Startup;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    void IFortressRuntimeHeadlessScenarioSessionPorts.ConfigureManualTicks(int initialCreatureCount)
    {
        if (initialCreatureCount < 0)
            throw new ArgumentOutOfRangeException(nameof(initialCreatureCount));

        _lifecycle.ConfigureManual((runtime, _) =>
        {
            runtime.AttachForManualTicks(systems =>
            {
                int existingCreatureCount = runtime.World.Creatures.InstanceCount;
                int spawnedCreatureCount = SimulationInitialWorkerSpawner.SpawnIfNeeded(
                    runtime.World,
                    initialCreatureCount,
                    _log);
                int actualCreatureCount = runtime.World.Creatures.InstanceCount;
                if (actualCreatureCount < initialCreatureCount)
                {
                    throw new InvalidOperationException(
                        $"Headless scenario requested {initialCreatureCount} initial creatures, but only "
                        + $"{actualCreatureCount} are available (existing={existingCreatureCount}, "
                        + $"spawned={spawnedCreatureCount}).");
                }

                systems.ProfessionAssignments.Initialize(runtime.World.Creatures.GetAllInstances());
            });
        });
    }

    void IFortressRuntimeHeadlessScenarioSessionPorts.ExecuteSingleTick()
    {
        if (_runtimeSession == null)
            throw new InvalidOperationException("World not initialized");
        if (_runtimeSession.Host.Systems == null)
            throw new InvalidOperationException("Manual tick systems have not been configured.");

        _services.TickScheduler.ExecuteSingleTick();
    }

    RuntimeHeadlessMetricsSnapshot IFortressRuntimeHeadlessScenarioSessionPorts.CaptureHeadlessMetrics()
    {
        var session = _runtimeSession
            ?? throw new InvalidOperationException("World not initialized");
        var systems = session.Host.RequireSystems();
        var transport = systems.TransportJobs.GetLastStatsSnapshot();
        var mining = systems.MiningJobs.GetLastStatsSnapshot();
        var craft = systems.CraftJobs.GetLastStatsSnapshot();
        var paths = session.Host.PathServices?.CaptureMetrics()
            ?? new RuntimePathMetricsSnapshot(
                InstrumentationIsComplete: false,
                RegisteredServiceCountCurrent: 0,
                InstrumentedServiceCountCurrent: 0,
                RequestsServedThisTick: 0,
                CacheHitsTotal: 0,
                CacheMissesTotal: 0,
                CacheEntriesCurrent: 0);
        var topology = session.Host.CaptureTopologyMetrics();
        var checkpoints = _committedCheckpoints.TryGetLatestIdentity(out var checkpointIdentity)
            ? new RuntimeCheckpointMetricsSnapshot(
                IsAvailable: true,
                RetainedCheckpointCountCurrent: _committedCheckpoints.RetainedCount,
                SectionCountCurrent: checkpointIdentity.Sections.Count,
                PayloadBytesCurrent: checkpointIdentity.Sections.Sum(static section => (long)section.PayloadLength))
            : new RuntimeCheckpointMetricsSnapshot(
                IsAvailable: false,
                RetainedCheckpointCountCurrent: _committedCheckpoints.RetainedCount,
                SectionCountCurrent: 0,
                PayloadBytesCurrent: 0);
        var schedulerHealth = _services.TickScheduler.CaptureHealthSnapshot();

        return new RuntimeHeadlessMetricsSnapshot(
            _services.TickScheduler.CurrentTick,
            session.World.Creatures.InstanceCount,
            session.World.Items.InstanceCount,
            paths,
            new RuntimePlannerMetricsSnapshot(
                TransportPlanningWorkerCountConfigured: systems.TransportJobs.PlanningWorkerCountConfigured,
                TransportPendingCurrent: systems.TransportQueue.Count,
                TransportIntakeThisTick: transport.Intake,
                TransportActiveCurrent: transport.Active,
                TransportBacklogCurrent: transport.Backlog,
                TransportCompletedThisTick: transport.CompletedDelta,
                TransportRequeuedThisTick: transport.RequeuedDelta,
                TransportNoPathThisTick: transport.NoPathDelta,
                MiningIntakeThisTick: mining.Intake,
                MiningActiveCurrent: mining.Active,
                MiningBacklogCurrent: mining.Backlog,
                MiningDeferredCurrent: mining.Deferred,
                MiningReservedTilesCurrent: mining.ReservedTiles,
                CraftIntakeThisTick: craft.Intake,
                CraftActiveCurrent: craft.Active,
                CraftBacklogCurrent: craft.Backlog,
                CraftCompletedThisTick: craft.CompletedDelta,
                ConstructionIntakeThisTick: systems.ConstructionJobs.LastIntakeCount,
                ConstructionSitesProcessedThisTick: systems.ConstructionJobs.LastProcessedSites),
            topology,
            checkpoints,
            schedulerHealth);
    }
}
