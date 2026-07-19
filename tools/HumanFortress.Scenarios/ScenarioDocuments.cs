using System.Text.Json;
using System.Text.Json.Serialization;

namespace HumanFortress.Scenarios;

internal static class ScenarioJson
{
    internal static JsonSerializerOptions Strict { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };
}

internal sealed record ScenarioProfileDocument(
    int SchemaVersion,
    string Id,
    string Description,
    ulong RuntimeSeed,
    ScenarioWorldDocument World,
    ScenarioWorkloadDocument Workload,
    int WarmupTicks,
    int MeasuredTicks,
    int CheckpointInterval,
    string CommandJournal);

internal sealed record ScenarioWorldDocument(
    string Mode,
    int SizeInChunks,
    int MaxZ,
    int StandableZ,
    uint GenerationSeed);

internal sealed record ScenarioWorkloadDocument(
    int InitialCreatures,
    int ItemInstances,
    int TransportRequests,
    string ItemDefinitionId);

internal sealed record ScenarioCommandJournalDocument(
    int SchemaVersion,
    string Id,
    string HashAlgorithm,
    string JournalHash,
    IReadOnlyList<ScenarioCommandRecordDocument> Records);

internal sealed record ScenarioCommandRecordDocument(
    ulong Tick,
    string CommandId,
    string CommandType,
    long? CommandIdentitySequence,
    string PayloadBase64);

internal sealed record ScenarioRunArtifact(
    int SchemaVersion,
    ScenarioArtifactIdentity Identity,
    ScenarioVariantEvidence Variant,
    ScenarioDeterministicEvidence Deterministic,
    ScenarioPerformanceEvidence Performance);

internal sealed record ScenarioArtifactIdentity(
    string ProfileId,
    string ProfileHash,
    string ScenarioHash,
    string JournalId,
    string JournalHash,
    string HashAlgorithm);

internal sealed record ScenarioVariantEvidence(
    int TransportPlanningWorkers,
    bool PrimeDerivedCaches,
    int ForceGcEveryTicks,
    bool ProcessWarm,
    string TieredCompilationSetting);

internal sealed record ScenarioDeterministicEvidence(
    ulong RuntimeSeed,
    uint GenerationSeed,
    int TotalTicks,
    string ContentSignature,
    string ContentMechanicalHash,
    ScenarioInitialAuthorityEvidence InitialAuthority,
    IReadOnlyList<ScenarioReplayCheckpointEvidence> ReplayCheckpoints,
    ScenarioFinalAuthorityEvidence FinalAuthority,
    ScenarioDeterministicCounters Counters);

internal sealed record ScenarioInitialAuthorityEvidence(
    int CreatureCount,
    int ItemInstancesSeeded,
    int TransportRequestsSeeded,
    int JournalRecordCount);

internal sealed record ScenarioReplayCheckpointEvidence(
    ulong Tick,
    string AggregateHash,
    string? WorldHash,
    string RngHash,
    string CommandLogHash,
    string PendingCommandLogHash,
    string? TransportHash,
    string? MiningHash,
    string? CraftHash);

internal sealed record ScenarioFinalAuthorityEvidence(
    int CreatureCount,
    int ItemInstanceCount,
    long TransportPending,
    long TransportActive,
    long TransportBacklog,
    long MiningActive,
    long MiningBacklog,
    long CraftActive,
    long CraftBacklog,
    long DirtyChunksProcessed,
    long NavigationChunkRebuilds,
    long SchedulerSystemFailures,
    int SchedulerQuarantinedSystems);

internal sealed record ScenarioDeterministicCounters(
    long PathRequestsServed,
    long TransportIntake,
    long TransportCompleted,
    long TransportRequeued,
    long TransportNoPath,
    long MiningIntake,
    long CraftIntake,
    long CraftCompleted,
    long ConstructionIntake,
    long ConstructionSitesProcessed);

internal sealed record ScenarioPerformanceEvidence(
    ScenarioEnvironmentEvidence Environment,
    ScenarioDistributionEvidence TickMicroseconds,
    ScenarioDistributionEvidence AllocatedBytesPerTick,
    long TotalAllocatedBytes,
    long PeakWorkingSetBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    long PathCacheHitsTotal,
    long PathCacheMissesTotal,
    long PathCacheEntriesCurrent,
    bool PathInstrumentationComplete,
    long CheckpointPayloadBytesCurrent,
    int CheckpointSectionCountCurrent,
    int RetainedCheckpointCountCurrent,
    ScenarioCachePrimeEvidence? CachePrime,
    IReadOnlyList<long> RawTickMicroseconds,
    IReadOnlyList<long> RawAllocatedBytesPerTick);

internal sealed record ScenarioEnvironmentEvidence(
    string OperatingSystem,
    string ProcessArchitecture,
    string FrameworkDescription,
    string GcMode,
    string TieredCompilationSetting,
    int ProcessorCount);

internal sealed record ScenarioDistributionEvidence(
    int SampleCount,
    long P50,
    long P95,
    long P99,
    long Maximum);

internal sealed record ScenarioCachePrimeEvidence(
    int RequestsIssued,
    int CompletePaths,
    long CacheHitsAdded);
