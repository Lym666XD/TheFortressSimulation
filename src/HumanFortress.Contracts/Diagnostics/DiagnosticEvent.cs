namespace HumanFortress.Contracts.Diagnostics;

/// <summary>
/// Immutable diagnostic event emitted by runtime, simulation, content, and UI code.
/// </summary>
public sealed class DiagnosticEvent
{
    public DiagnosticEvent(
        DiagnosticLevel level,
        string category,
        string message,
        DateTimeOffset timestampUtc,
        long sequence,
        int managedThreadId,
        ulong? tick = null,
        string? exception = null)
    {
        Level = level;
        Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
        Message = message ?? string.Empty;
        TimestampUtc = timestampUtc;
        Sequence = sequence;
        ManagedThreadId = managedThreadId;
        Tick = tick;
        Exception = exception;
    }

    public DiagnosticLevel Level { get; }

    public string Category { get; }

    public string Message { get; }

    public DateTimeOffset TimestampUtc { get; }

    public long Sequence { get; }

    public int ManagedThreadId { get; }

    public ulong? Tick { get; }

    public string? Exception { get; }

    public static DiagnosticEvent Create(
        DiagnosticLevel level,
        string category,
        string message,
        Exception? exception = null,
        ulong? tick = null)
    {
        return new DiagnosticEvent(
            level,
            category,
            message,
            DateTimeOffset.UtcNow,
            0,
            Environment.CurrentManagedThreadId,
            tick,
            exception?.ToString());
    }

    public DiagnosticEvent WithSequence(long sequence)
    {
        return new DiagnosticEvent(
            Level,
            Category,
            Message,
            TimestampUtc,
            sequence,
            ManagedThreadId,
            Tick,
            Exception);
    }
}
