using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotDocumentCommandMapper
{
    internal static IReadOnlyList<CommandReplayRecord> ToExecutedCommandReplayRecords(
        RuntimeSaveSnapshotDocumentData document)
    {
        return ToCommandReplayRecords(document.ExecutedCommandRecords, "executed");
    }

    internal static IReadOnlyList<CommandReplayRecord> ToPendingCommandReplayRecords(
        RuntimeSaveSnapshotDocumentData document)
    {
        return ToCommandReplayRecords(document.PendingCommandRecords, "pending");
    }

    private static IReadOnlyList<CommandReplayRecord> ToCommandReplayRecords(
        IEnumerable<RuntimeSaveCommandRecordData>? records,
        string sectionName)
    {
        if (records == null)
            throw new InvalidDataException($"Save snapshot document is missing {sectionName} command records.");

        return records
            .Select((record, index) => ToCommandReplayRecord(record, sectionName, index))
            .ToArray();
    }

    private static CommandReplayRecord ToCommandReplayRecord(
        RuntimeSaveCommandRecordData record,
        string sectionName,
        int index)
    {
        if (string.IsNullOrWhiteSpace(record.CommandType))
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} has a blank command type.");
        if (record.PayloadLength < 0)
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} has a negative payload length.");
        if (record.PayloadBase64 == null)
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} is missing payload bytes.");

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(record.PayloadBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} payload is not valid base64.", ex);
        }

        if (payload.Length != record.PayloadLength)
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} payload length does not match payload bytes.");

        try
        {
            return new CommandReplayRecord(
                record.Tick,
                record.CommandId,
                record.CommandType,
                payload,
                record.CommandIdentitySequence);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} contains invalid replay metadata.", ex);
        }
    }
}
