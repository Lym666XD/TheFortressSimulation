namespace HumanFortress.Jobs.Transport;

internal readonly record struct TransportJobRestoreResult(
    bool Success,
    string[] Issues)
{
    internal static TransportJobRestoreResult Successful { get; } = new(true, Array.Empty<string>());
}
