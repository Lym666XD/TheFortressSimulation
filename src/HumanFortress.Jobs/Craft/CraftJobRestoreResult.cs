namespace HumanFortress.Jobs.Craft;

internal readonly record struct CraftJobRestoreResult(
    bool Success,
    string[] Issues)
{
    internal static CraftJobRestoreResult Successful { get; } = new(true, Array.Empty<string>());
}
