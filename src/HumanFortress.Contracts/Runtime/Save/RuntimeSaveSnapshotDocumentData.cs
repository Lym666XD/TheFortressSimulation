using HumanFortress.Contracts.Simulation.Save;

namespace HumanFortress.Contracts.Runtime.Save;

public readonly record struct RuntimeSaveSnapshotDocumentData(
    RuntimeSaveManifestData Manifest,
    WorldSavePayloadData? WorldPayload,
    RuntimeSaveRngStreamRecordData[] RngStreams,
    RuntimeSaveCommandRecordData[] ExecutedCommandRecords,
    RuntimeSaveCommandRecordData[] PendingCommandRecords);

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
