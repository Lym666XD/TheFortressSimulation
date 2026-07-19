using HumanFortress.Contracts.Simulation.Save;

namespace HumanFortress.Contracts.Runtime.Save;

public readonly record struct RuntimeSaveSnapshotDocumentData(
    RuntimeSaveManifestData Manifest,
    WorldSavePayloadData? WorldPayload,
    RuntimeSaveMiningJobsData? MiningJobs,
    RuntimeSaveTransportJobsData? TransportJobs,
    RuntimeSaveCraftJobsData? CraftJobs,
    RuntimeSaveRngStreamRecordData[] RngStreams,
    RuntimeSaveCommandRecordData[] ExecutedCommandRecords,
    RuntimeSaveCommandRecordData[] PendingCommandRecords);

public readonly record struct RuntimeSaveMiningJobsData(
    RuntimeSaveMiningActiveJobData[] ActiveJobs,
    RuntimeSaveMiningBacklogEntryData[] BacklogEntries,
    RuntimeSaveMiningDeferredStairwellData[] DeferredStairwells,
    RuntimeSaveMiningReservedTileData[] ReservedTiles,
    RuntimeSaveMiningRecentCompletionData[] RecentCompletions);

public readonly record struct RuntimeSaveMiningActiveJobData(
    int Order,
    Guid WorkerId,
    int TargetX,
    int TargetY,
    int Z,
    int AdjacentX,
    int AdjacentY,
    int Stage,
    int ProgressTicks,
    int RequiredTicks,
    int GeologyHandle,
    int TerrainKind,
    int Priority,
    ulong AssignedTick,
    int ReplanFailCount,
    int Action,
    int Segment,
    int DesignationId);

public readonly record struct RuntimeSaveMiningBacklogEntryData(
    int Order,
    RuntimeSavePlannedMiningDigData Dig,
    ulong EnqueuedTick);

public readonly record struct RuntimeSaveMiningDeferredStairwellData(
    int Order,
    RuntimeSavePlannedMiningDigData Dig);

public readonly record struct RuntimeSaveMiningReservedTileData(
    int X,
    int Y,
    int Z);

public readonly record struct RuntimeSaveMiningRecentCompletionData(
    int Order,
    int X,
    int Y,
    int Z,
    ulong ExpireTick);

public readonly record struct RuntimeSavePlannedMiningDigData(
    int X,
    int Y,
    int Z,
    int GeologyHandle,
    int TerrainKind,
    int Priority,
    ulong Seed,
    int Action,
    int Segment,
    int DesignationId);

public readonly record struct RuntimeSaveTransportJobsData(
    int? IntakeCapHint,
    int? MaxActiveCapHint,
    int ReserveSlotsHint,
    RuntimeSaveTransportRequestData[] PendingRequests,
    RuntimeSaveTransportActiveJobData[] ActiveJobs,
    RuntimeSaveTransportBacklogEntryData[] BacklogEntries);

public readonly record struct RuntimeSaveTransportRequestData(
    Guid ItemGuid,
    int FromX,
    int FromY,
    int FromZ,
    int ToX,
    int ToY,
    int ToZ,
    int Quantity,
    int Reason,
    int Priority,
    string RequestorId,
    ulong CreatedTick,
    uint Seed);

public readonly record struct RuntimeSaveTransportActiveJobData(
    int Order,
    Guid CreatureId,
    Guid ItemId,
    int DestinationX,
    int DestinationY,
    int DestinationZ,
    int Stage,
    int Quantity,
    int InvalidReplanCount,
    int Reason);

public readonly record struct RuntimeSaveTransportBacklogEntryData(
    int Order,
    RuntimeSaveTransportRequestData Request,
    ulong EnqueuedTick);

public readonly record struct RuntimeSaveCraftJobsData(
    RuntimeSaveCraftActiveJobData[] ActiveJobs,
    RuntimeSaveCraftBacklogEntryData[] BacklogEntries);

public readonly record struct RuntimeSaveCraftActiveJobData(
    int Order,
    Guid WorkerId,
    Guid WorkshopGuid,
    Guid QueueEntryId,
    string RecipeId,
    int Stage,
    int WorkTicksRemaining,
    int AnchorX,
    int AnchorY,
    int Z);

public readonly record struct RuntimeSaveCraftBacklogEntryData(
    int Order,
    RuntimeSavePlannedCraftJobData Job);

public readonly record struct RuntimeSavePlannedCraftJobData(
    Guid WorkshopGuid,
    Guid QueueEntryId,
    string RecipeId,
    int DurationTicks,
    int AnchorX,
    int AnchorY,
    int Z);

public readonly record struct RuntimeSaveRngStreamRecordData(
    string StreamName,
    uint S0,
    uint S1,
    uint S2,
    uint S3);

public readonly record struct RuntimeSaveCommandRecordData(
    ulong Tick,
    Guid CommandId,
    string CommandType,
    string PayloadBase64,
    int PayloadLength,
    long? CommandIdentitySequence);
