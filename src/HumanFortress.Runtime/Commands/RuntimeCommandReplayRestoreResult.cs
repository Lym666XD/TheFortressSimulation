namespace HumanFortress.Runtime.Commands;

internal sealed class RuntimeCommandReplayRestoreResult
{
    private readonly RuntimeCommandReplayRestoreIssue[] _issues;

    private RuntimeCommandReplayRestoreResult(
        int recordCount,
        int restoredCommandCount,
        long maxCommandIdentitySequence,
        RuntimeCommandReplayRestoreIssue[] issues)
    {
        RecordCount = recordCount;
        RestoredCommandCount = restoredCommandCount;
        MaxCommandIdentitySequence = maxCommandIdentitySequence;
        _issues = issues;
    }

    internal int RecordCount { get; }
    internal int RestoredCommandCount { get; }
    internal long MaxCommandIdentitySequence { get; }
    internal bool Success => _issues.Length == 0;
    internal IReadOnlyList<RuntimeCommandReplayRestoreIssue> Issues => _issues;

    internal static RuntimeCommandReplayRestoreResult Succeeded(
        int recordCount,
        int restoredCommandCount,
        long maxCommandIdentitySequence)
    {
        return new RuntimeCommandReplayRestoreResult(
            recordCount,
            restoredCommandCount,
            maxCommandIdentitySequence,
            Array.Empty<RuntimeCommandReplayRestoreIssue>());
    }

    internal static RuntimeCommandReplayRestoreResult Failed(
        int recordCount,
        IReadOnlyList<RuntimeCommandReplayRestoreIssue> issues)
    {
        return new RuntimeCommandReplayRestoreResult(
            recordCount,
            restoredCommandCount: 0,
            maxCommandIdentitySequence: 0,
            issues.ToArray());
    }
}
