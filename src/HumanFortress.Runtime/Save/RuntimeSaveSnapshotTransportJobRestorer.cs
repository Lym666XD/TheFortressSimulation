using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Save;

internal readonly record struct RuntimeSaveTransportJobRestoreResult(
    bool Success,
    int RestoredRecordCount,
    RuntimeSaveSnapshotDocumentIssueData[] RestoreIssues);

internal static class RuntimeSaveSnapshotTransportJobRestorer
{
    internal static RuntimeSaveTransportJobRestoreResult Restore(
        FortressRuntimeSession? session,
        RuntimeSaveSnapshotDocumentData document)
    {
        if (document.TransportJobs is not { } payload)
            return new RuntimeSaveTransportJobRestoreResult(true, 0, Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());

        if (session == null)
        {
            return Fail("Transport job payload cannot be restored without an active Runtime session.");
        }

        try
        {
            var queue = RuntimeSaveSnapshotDocumentTransportMapper.ToQueueSnapshot(payload);
            var executor = RuntimeSaveSnapshotDocumentTransportMapper.ToReplaySnapshot(payload);
            var systems = session.Host.RequireSystems();
            var result = systems.TransportJobs.RestoreReplaySnapshot(queue, executor);
            if (!result.Success)
            {
                return new RuntimeSaveTransportJobRestoreResult(
                    false,
                    0,
                    result.Issues
                        .Select(static issue => new RuntimeSaveSnapshotDocumentIssueData(
                            RuntimeSaveManifestSections.JobsTransport,
                            null,
                            issue))
                        .ToArray());
            }

            return new RuntimeSaveTransportJobRestoreResult(
                true,
                RuntimeSaveSnapshotDocumentTransportMapper.CountRecords(payload),
                Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }
        catch (InvalidDataException ex)
        {
            return Fail(ex.Message);
        }
    }

    private static RuntimeSaveTransportJobRestoreResult Fail(string message)
    {
        return new RuntimeSaveTransportJobRestoreResult(
            false,
            0,
            new[]
            {
                new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsTransport,
                    null,
                    message)
            });
    }
}
