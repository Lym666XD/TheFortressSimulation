namespace HumanFortress.App;

internal static partial class LegacyLogCategoryResolver
{
    public static string Resolve(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "App.Legacy";
        }

        var prefix = ExtractBracketPrefix(message);
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            return PrefixToCategory(prefix);
        }

        if (message.Contains("ContentRegistry", StringComparison.OrdinalIgnoreCase))
        {
            return "Content.Registry";
        }

        if (message.Contains("ItemManager", StringComparison.OrdinalIgnoreCase))
        {
            return "Simulation.Items";
        }

        if (message.Contains("CreatureManager", StringComparison.OrdinalIgnoreCase))
        {
            return "Simulation.Creatures";
        }

        return "App.Legacy";
    }

    private static string? ExtractBracketPrefix(string message)
    {
        if (message.Length < 3 || message[0] != '[')
        {
            return null;
        }

        var end = message.IndexOf(']', StringComparison.Ordinal);
        if (end <= 1)
        {
            return null;
        }

        return message.Substring(1, end - 1);
    }

}
