using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Contracts.Runtime.Checkpoints;

public readonly record struct RuntimeCommittedDiagnosticsFactsData(
    SimulationSnapshotMetadata Metadata,
    bool IsPaused,
    float SpeedMultiplier,
    bool HasWorld,
    int RngStreamCount,
    int ExecutedCommandCount,
    int PendingCommandCount,
    int TransportRecordCount,
    int MiningRecordCount,
    int CraftRecordCount,
    int ProfessionRecordCount);

public readonly record struct RuntimeCommittedDiagnosticsData(
    RuntimeCheckpointIdentityData CheckpointIdentity,
    RuntimeCommittedDiagnosticsFactsData Facts);
