using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Save;

internal readonly record struct RuntimeSaveMiningJobRestoreResult(
    bool Success,
    int RestoredRecordCount,
    RuntimeSaveSnapshotDocumentIssueData[] RestoreIssues);

internal static class RuntimeSaveSnapshotMiningJobRestorer
{
    internal static RuntimeSaveMiningJobRestoreResult Restore(
        FortressRuntimeSession? session,
        RuntimeSaveSnapshotDocumentData document)
    {
        if (document.MiningJobs is not { } payload)
            return new RuntimeSaveMiningJobRestoreResult(true, 0, Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());

        if (session == null)
        {
            return Fail("Mining job payload cannot be restored without an active Runtime session.");
        }

        try
        {
            var snapshot = RuntimeSaveSnapshotDocumentMiningMapper.ToReplaySnapshot(payload);
            var systems = session.Host.RequireSystems();
            var result = systems.MiningJobs.RestoreReplaySnapshot(snapshot);
            if (!result.Success)
            {
                return new RuntimeSaveMiningJobRestoreResult(
                    false,
                    0,
                    result.Issues
                        .Select(static issue => new RuntimeSaveSnapshotDocumentIssueData(
                            RuntimeSaveManifestSections.JobsMining,
                            null,
                            issue))
                        .ToArray());
            }

            return new RuntimeSaveMiningJobRestoreResult(
                true,
                RuntimeSaveSnapshotDocumentMiningMapper.CountRecords(payload),
                Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }
        catch (InvalidDataException ex)
        {
            return Fail(ex.Message);
        }
    }

    private static RuntimeSaveMiningJobRestoreResult Fail(string message)
    {
        return new RuntimeSaveMiningJobRestoreResult(
            false,
            0,
            new[]
            {
                new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsMining,
                    null,
                    message)
            });
    }
}
