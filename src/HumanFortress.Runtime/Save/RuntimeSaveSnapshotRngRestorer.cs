using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Core.Determinism;
using HumanFortress.Core.Random;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Save;

internal readonly record struct RuntimeSaveRngSnapshotRestoreResult(
    bool Success,
    int RestoredStreamCount,
    RuntimeSaveSnapshotDocumentIssueData[] RestoreIssues);

internal static class RuntimeSaveSnapshotRngRestorer
{
    internal static RuntimeSaveRngSnapshotRestoreResult Restore(
        RuntimeSessionServices services,
        RuntimeSaveSnapshotDocumentData document)
    {
        ArgumentNullException.ThrowIfNull(services);

        IReadOnlyList<RngStreamStateSnapshot> streams;
        try
        {
            streams = RuntimeSaveSnapshotDocumentRngMapper.ToRngStreamStateSnapshots(document);
        }
        catch (InvalidDataException ex)
        {
            return Failure(ex.Message);
        }

        services.RngStreams.ClearStreams();
        try
        {
            services.RngStreams.RestoreStates(streams);
        }
        catch (ArgumentException ex)
        {
            return Failure(ex.Message);
        }

        var restoredHash = RngReplayHashBuilder.Build(services.RngStreams);
        if (!string.Equals(restoredHash, document.Manifest.Checkpoint.RngHash, StringComparison.Ordinal))
            return Failure("Restored RNG stream hash does not match the save snapshot checkpoint RNG hash.");

        return new RuntimeSaveRngSnapshotRestoreResult(
            Success: true,
            RestoredStreamCount: streams.Count,
            RestoreIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
    }

    private static RuntimeSaveRngSnapshotRestoreResult Failure(string message)
    {
        return new RuntimeSaveRngSnapshotRestoreResult(
            Success: false,
            RestoredStreamCount: 0,
            RestoreIssues: new[]
            {
                new RuntimeSaveSnapshotDocumentIssueData(
                    "rng",
                    null,
                    message)
            });
    }
}
