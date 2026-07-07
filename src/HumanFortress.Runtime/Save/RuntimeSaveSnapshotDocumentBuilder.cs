using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Random;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotDocumentBuilder
{
    internal static RuntimeSaveSnapshotDocumentData Build(RuntimeSaveSnapshotData snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new RuntimeSaveSnapshotDocumentData(
            snapshot.Manifest,
            snapshot.WorldPayload,
            snapshot.RngStreams.Select(ToDocumentRecord).ToArray(),
            snapshot.CommandReplayRecords.Select(ToDocumentRecord).ToArray(),
            snapshot.PendingCommandReplayRecords.Select(ToDocumentRecord).ToArray());
    }

    private static RuntimeSaveRngStreamRecordData ToDocumentRecord(RngStreamStateSnapshot stream)
    {
        if (string.IsNullOrWhiteSpace(stream.StreamName))
            throw new InvalidDataException("Runtime save snapshot contains a blank RNG stream name.");

        return new RuntimeSaveRngStreamRecordData(
            stream.StreamName,
            stream.State.S0,
            stream.State.S1,
            stream.State.S2,
            stream.State.S3);
    }

    private static RuntimeSaveCommandRecordData ToDocumentRecord(CommandReplayRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new RuntimeSaveCommandRecordData(
            record.Tick,
            record.CommandId,
            record.CommandType,
            Convert.ToBase64String(record.Payload.Span),
            record.PayloadLength,
            record.CommandIdentitySequence);
    }
}
