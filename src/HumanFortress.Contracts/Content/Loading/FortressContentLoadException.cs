namespace HumanFortress.Contracts.Content.Loading;

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
