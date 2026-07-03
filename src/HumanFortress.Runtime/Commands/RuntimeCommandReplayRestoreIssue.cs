namespace HumanFortress.Runtime.Commands;

internal readonly record struct RuntimeCommandReplayRestoreIssue(
    int RecordIndex,
    string CommandType,
    string Message);
