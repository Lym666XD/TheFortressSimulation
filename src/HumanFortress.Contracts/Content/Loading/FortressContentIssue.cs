namespace HumanFortress.Contracts.Content.Loading;

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
