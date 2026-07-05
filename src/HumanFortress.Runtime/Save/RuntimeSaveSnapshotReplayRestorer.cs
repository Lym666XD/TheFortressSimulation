using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotReplayRestorer
{
    internal static RuntimeSaveSnapshotRestoreResultData RestorePendingCommands(
        RuntimeSessionServices services,
        RuntimeSaveSnapshotDocumentData document,
        RuntimeSaveSnapshotDocumentValidationResultData? prevalidated = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var validation = prevalidated ?? RuntimeSaveSnapshotDocumentVerifier.Validate(document);
        if (!validation.Success)
        {
            return new RuntimeSaveSnapshotRestoreResultData(
                Success: false,
                Validation: validation,
                PendingRecordCount: 0,
                RestoredCommandCount: 0,
                MaxCommandIdentitySequence: 0,
                RestoreIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }

        var pendingRecords = RuntimeSaveSnapshotDocumentCommandMapper.ToPendingCommandReplayRecords(document);
        var restore = new RuntimeCommandReplayRestorer().RestorePending(services, pendingRecords);
        var restoreIssues = restore.Issues
            .Select(issue => new RuntimeSaveSnapshotDocumentIssueData(
                "commands.pending.replay",
                issue.RecordIndex,
                issue.Message))
            .ToArray();

        return new RuntimeSaveSnapshotRestoreResultData(
            Success: validation.Success && restore.Success,
            Validation: validation,
            PendingRecordCount: pendingRecords.Count,
            RestoredCommandCount: restore.RestoredCommandCount,
            MaxCommandIdentitySequence: restore.MaxCommandIdentitySequence,
            RestoreIssues: restoreIssues);
    }
}
