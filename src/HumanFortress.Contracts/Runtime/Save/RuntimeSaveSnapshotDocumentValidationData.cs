namespace HumanFortress.Contracts.Runtime.Save;

public readonly record struct RuntimeSaveSnapshotDocumentIssueData(
    string Section,
    int? RecordIndex,
    string Message);

public readonly record struct RuntimeSaveSnapshotDocumentValidationResultData(
    bool Success,
    RuntimeSaveSnapshotDocumentIssueData[] Issues)
{
    public static RuntimeSaveSnapshotDocumentValidationResultData Valid { get; } = new(
        Success: true,
        Issues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
}

public readonly record struct RuntimeSaveSnapshotRestoreResultData(
    bool Success,
    RuntimeSaveSnapshotDocumentValidationResultData Validation,
    int PendingRecordCount,
    int RestoredCommandCount,
    long MaxCommandIdentitySequence,
    RuntimeSaveSnapshotDocumentIssueData[] RestoreIssues);

public readonly record struct RuntimeSaveWorldSnapshotRestoreResultData(
    bool Success,
    RuntimeSaveSnapshotDocumentValidationResultData Validation,
    string SavedWorldHash,
    string RestoredWorldHash,
    int RestoredChunkCount,
    int RestoredTileCount,
    RuntimeSaveSnapshotDocumentIssueData[] RestoreIssues);

public readonly record struct RuntimeSaveFullSnapshotRestoreResultData(
    bool Success,
    RuntimeSaveSnapshotDocumentValidationResultData Validation,
    string SavedWorldHash,
    string RestoredWorldHash,
    int RestoredChunkCount,
    int RestoredTileCount,
    int RestoredRngStreamCount,
    int PendingRecordCount,
    int RestoredCommandCount,
    long MaxCommandIdentitySequence,
    RuntimeSaveSnapshotDocumentIssueData[] RestoreIssues);
