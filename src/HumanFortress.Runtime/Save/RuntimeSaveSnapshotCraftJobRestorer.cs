using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Save;

internal readonly record struct RuntimeSaveCraftJobRestoreResult(
    bool Success,
    int RestoredRecordCount,
    RuntimeSaveSnapshotDocumentIssueData[] RestoreIssues);

internal static class RuntimeSaveSnapshotCraftJobRestorer
{
    internal static RuntimeSaveCraftJobRestoreResult Restore(
        FortressRuntimeSession? session,
        RuntimeSaveSnapshotDocumentData document)
    {
        if (document.CraftJobs is not { } payload)
            return new RuntimeSaveCraftJobRestoreResult(true, 0, Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());

        if (session == null)
        {
            return Fail("Craft job payload cannot be restored without an active Runtime session.");
        }

        try
        {
            var snapshot = RuntimeSaveSnapshotDocumentCraftMapper.ToReplaySnapshot(payload);
            var systems = session.Host.RequireSystems();
            var result = systems.CraftJobs.RestoreReplaySnapshot(snapshot);
            if (!result.Success)
            {
                return new RuntimeSaveCraftJobRestoreResult(
                    false,
                    0,
                    result.Issues
                        .Select(static issue => new RuntimeSaveSnapshotDocumentIssueData(
                            RuntimeSaveManifestSections.JobsCraft,
                            null,
                            issue))
                        .ToArray());
            }

            return new RuntimeSaveCraftJobRestoreResult(
                true,
                RuntimeSaveSnapshotDocumentCraftMapper.CountRecords(payload),
                Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }
        catch (InvalidDataException ex)
        {
            return Fail(ex.Message);
        }
    }

    private static RuntimeSaveCraftJobRestoreResult Fail(string message)
    {
        return new RuntimeSaveCraftJobRestoreResult(
            false,
            0,
            new[]
            {
                new RuntimeSaveSnapshotDocumentIssueData(
                    RuntimeSaveManifestSections.JobsCraft,
                    null,
                    message)
            });
    }
}
