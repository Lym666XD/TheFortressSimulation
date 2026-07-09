namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct JobPoint3(int X, int Y, int Z);

public readonly record struct JobStatsView(
    int Intake,
    int Active,
    int Backlog,
    int CompletedDelta,
    int RequeuedDelta,
    int NoPathDelta,
    int Deferred,
    int ReservedTiles,
    int CarryoverOld);

public readonly record struct ConstructionJobStatusView(
    int LastProcessedSites,
    int LastIntakeCount,
    int IntakeLimit);

public readonly record struct JobsSchedulerStatsView(
    ulong Tick,
    long PlanMsTotal,
    long ApplyMsTotal,
    int IntakeHaul,
    int IntakeMining,
    int IntakeConstruction,
    int IntakeCraft);

public readonly record struct ActiveJobView(
    string Kind,
    Guid WorkerId,
    string Stage,
    string Target);

public readonly record struct TransportRequestDebugView(
    string Reason,
    JobPoint3 To);

public readonly record struct TransportShardCountView(
    int ShardId,
    int Count);

public readonly record struct TransportDebugView(
    IReadOnlyList<TransportRequestDebugView> PendingPeek,
    IReadOnlyList<TransportShardCountView> ShardCounts);

public readonly record struct SimulationJobsDebugData(
    ulong Tick,
    JobStatsView? Transport,
    JobStatsView? Mining,
    JobStatsView? Craft,
    ConstructionJobStatusView Construction,
    JobsSchedulerStatsView? Scheduler,
    TransportDebugView? TransportDebug,
    IReadOnlyList<ActiveJobView> ActiveJobs,
    IReadOnlyList<JobPoint3> ActiveMiningTargets,
    IReadOnlyList<JobPoint3> RecentMiningCompletions);

public readonly record struct ProfessionDefinitionView(string Id, string Name);

public readonly record struct ProfessionRosterEntryView(
    Guid WorkerId,
    string Name,
    bool IsAvailable,
    IReadOnlyDictionary<string, int> Weights);

public readonly record struct WorkforceDebugData(
    IReadOnlyList<ProfessionDefinitionView> Professions,
    IReadOnlyList<ProfessionRosterEntryView> Roster,
    int TotalWorkers,
    int AvailableWorkers);
