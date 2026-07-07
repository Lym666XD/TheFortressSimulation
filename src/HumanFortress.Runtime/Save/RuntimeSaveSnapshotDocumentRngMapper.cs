using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Core.Random;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotDocumentRngMapper
{
    internal static IReadOnlyList<RngStreamStateSnapshot> ToRngStreamStateSnapshots(
        RuntimeSaveSnapshotDocumentData document)
    {
        return ToRngStreamStateSnapshots(document.RngStreams);
    }

    private static IReadOnlyList<RngStreamStateSnapshot> ToRngStreamStateSnapshots(
        IEnumerable<RuntimeSaveRngStreamRecordData>? records)
    {
        if (records == null)
            throw new InvalidDataException("Save snapshot document is missing RNG stream records.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return records
            .Select((record, index) => ToRngStreamStateSnapshot(record, index, seen))
            .OrderBy(static stream => stream.StreamName, StringComparer.Ordinal)
            .ToArray();
    }

    private static RngStreamStateSnapshot ToRngStreamStateSnapshot(
        RuntimeSaveRngStreamRecordData record,
        int index,
        ISet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(record.StreamName))
            throw new InvalidDataException($"Save snapshot RNG stream {index} has a blank stream name.");
        if (!seen.Add(record.StreamName))
            throw new InvalidDataException($"Save snapshot RNG stream {index} duplicates stream '{record.StreamName}'.");

        return new RngStreamStateSnapshot(
            record.StreamName,
            new RngState(record.S0, record.S1, record.S2, record.S3));
    }
}
