using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Contracts.Runtime.Replay;

public readonly record struct RuntimeReplayCheckpointData(
    SimulationSnapshotMetadata Metadata,
    string AggregateHash,
    string? WorldHash,
    string RngHash,
    int RngStreamCount,
    string CommandLogHash,
    int CommandLogRecordCount,
    string PendingCommandLogHash,
    int PendingCommandLogRecordCount,
    string? TransportHash,
    string? MiningHash,
    string? CraftHash);
