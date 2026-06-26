namespace HumanFortress.Runtime;

internal sealed partial class StockpileCommandTarget
{
    private string BuildZoneName(string presetId)
    {
        int number = _world.Stockpiles.GetAllZones().Count() + 1;
        return presetId.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? $"Stockpile {number}"
            : $"{ToTitle(presetId)} Stockpile {number}";
    }

    private static string ToTitle(string value)
    {
        return value.Length == 0
            ? "Stockpile"
            : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
