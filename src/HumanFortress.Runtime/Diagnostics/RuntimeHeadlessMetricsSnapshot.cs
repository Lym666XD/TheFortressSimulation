using HumanFortress.Core.Time;

namespace HumanFortress.Runtime.Diagnostics;

internal readonly record struct RuntimePathMetricsSnapshot(
    bool InstrumentationIsComplete,
    int RegisteredServiceCountCurrent,
    int InstrumentedServiceCountCurrent,
    long RequestsServedThisTick,
    long CacheHitsTotal,
    long CacheMissesTotal,
    long CacheEntriesCurrent);

internal readonly record struct RuntimePlannerMetricsSnapshot(
    int TransportPlanningWorkerCountConfigured,
    long TransportPendingCurrent,
    long TransportIntakeThisTick,
    long TransportActiveCurrent,
    long TransportBacklogCurrent,
    long TransportCompletedThisTick,
    long TransportRequeuedThisTick,
    long TransportNoPathThisTick,
    long MiningIntakeThisTick,
    long MiningActiveCurrent,
    long MiningBacklogCurrent,
    long MiningDeferredCurrent,
    long MiningReservedTilesCurrent,
    long CraftIntakeThisTick,
    long CraftActiveCurrent,
    long CraftBacklogCurrent,
    long CraftCompletedThisTick,
    long ConstructionIntakeThisTick,
    long ConstructionSitesProcessedThisTick);

internal readonly record struct RuntimeTopologyMetricsSnapshot(
    long DirtyChunksProcessedTotal,
    long NavigationChunkRebuildsTotal);

internal readonly record struct RuntimeCheckpointMetricsSnapshot(
    bool IsAvailable,
    int RetainedCheckpointCountCurrent,
    int SectionCountCurrent,
    long PayloadBytesCurrent);

internal readonly record struct RuntimeHeadlessMetricsSnapshot(
    ulong CurrentTick,
    int CreatureCountCurrent,
    int ItemInstanceCountCurrent,
    RuntimePathMetricsSnapshot Paths,
    RuntimePlannerMetricsSnapshot Planners,
    RuntimeTopologyMetricsSnapshot Topology,
    RuntimeCheckpointMetricsSnapshot Checkpoints,
    TickSchedulerHealthSnapshot SchedulerHealth);

internal readonly record struct RuntimeHeadlessWorkloadRequest(
    string ItemDefinitionId,
    int ItemInstanceCount,
    int TransportRequestCount,
    int Z);

internal readonly record struct RuntimeHeadlessWorkloadResult(
    int ItemInstancesSeeded,
    int TransportRequestsSeeded);

internal readonly record struct RuntimeHeadlessCachePrimeResult(
    int RequestsIssued,
    int CompletePaths,
    long CacheHitsAdded);
