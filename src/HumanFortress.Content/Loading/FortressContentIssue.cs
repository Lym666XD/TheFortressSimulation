namespace HumanFortress.Content.Loading;

public enum FortressContentIssueSeverity
{
    Warning,
    Error
}

public sealed class FortressContentIssue
{
    public FortressContentIssue(
        FortressContentIssueSeverity severity,
        string code,
        string message)
    {
        Severity = severity;
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }

    public FortressContentIssueSeverity Severity { get; }
    public string Code { get; }
    public string Message { get; }

    public override string ToString()
    {
        return $"{Severity} {Code}: {Message}";
    }
}

public sealed class FortressContentLoadException : InvalidOperationException
{
    public FortressContentLoadException(IReadOnlyList<FortressContentIssue> blockingIssues)
        : base(CreateMessage(blockingIssues))
    {
        BlockingIssues = blockingIssues?.ToArray() ?? throw new ArgumentNullException(nameof(blockingIssues));
    }

    public IReadOnlyList<FortressContentIssue> BlockingIssues { get; }

    private static string CreateMessage(IReadOnlyList<FortressContentIssue>? blockingIssues)
    {
        if (blockingIssues == null || blockingIssues.Count == 0)
        {
            return "Content load failed.";
        }

        return "Content load failed:" + Environment.NewLine + string.Join(Environment.NewLine, blockingIssues);
    }
}
