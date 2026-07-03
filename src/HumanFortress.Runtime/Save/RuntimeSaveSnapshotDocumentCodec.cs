using System.Text.Json;
using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

public static class RuntimeSaveSnapshotDocumentCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Serialize(RuntimeSaveSnapshotDocumentData document)
    {
        Validate(document);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public static RuntimeSaveSnapshotDocumentData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Save snapshot document JSON must not be blank.", nameof(json));

        var document = JsonSerializer.Deserialize<RuntimeSaveSnapshotDocumentData>(json, JsonOptions);
        Validate(document);
        return document;
    }

    private static void Validate(RuntimeSaveSnapshotDocumentData document)
    {
        if (document.ExecutedCommandRecords == null)
            throw new InvalidDataException("Save snapshot document is missing executed command records.");
        if (document.PendingCommandRecords == null)
            throw new InvalidDataException("Save snapshot document is missing pending command records.");
        if (document.RngStreams == null)
            throw new InvalidDataException("Save snapshot document is missing RNG stream records.");

        ValidateRngStreams(document.RngStreams);
        ValidateCommandRecords(document.ExecutedCommandRecords, "executed");
        ValidateCommandRecords(document.PendingCommandRecords, "pending");
    }

    private static void ValidateRngStreams(IEnumerable<RuntimeSaveRngStreamRecordData> records)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.StreamName))
                throw new InvalidDataException($"Save snapshot RNG stream {index} has a blank stream name.");
            if (!seen.Add(record.StreamName))
                throw new InvalidDataException($"Save snapshot RNG stream {index} duplicates stream '{record.StreamName}'.");
            index++;
        }
    }

    private static void ValidateCommandRecords(
        IEnumerable<RuntimeSaveCommandRecordData> records,
        string sectionName)
    {
        var index = 0;
        foreach (var record in records)
        {
            ValidateCommandRecord(record, sectionName, index);
            index++;
        }
    }

    private static void ValidateCommandRecord(
        RuntimeSaveCommandRecordData record,
        string sectionName,
        int index)
    {
        if (string.IsNullOrWhiteSpace(record.CommandType))
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} has a blank command type.");
        if (record.PayloadLength < 0)
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} has a negative payload length.");
        if (record.CommandIdentitySequence.HasValue && record.CommandIdentitySequence.Value <= 0)
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} has an invalid command identity sequence.");
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
    }
}
