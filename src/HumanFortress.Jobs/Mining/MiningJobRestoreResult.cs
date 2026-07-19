namespace HumanFortress.Jobs.Mining;

internal readonly record struct MiningJobRestoreResult(
    bool Success,
    string[] Issues)
{
    internal static MiningJobRestoreResult Successful { get; } = new(true, Array.Empty<string>());
}
