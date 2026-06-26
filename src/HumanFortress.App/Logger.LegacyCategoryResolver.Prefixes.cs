namespace HumanFortress.App;

internal static partial class LegacyLogCategoryResolver
{
    private static string PrefixToCategory(string prefix)
    {
        if (IsContentPrefix(prefix))
            return "Content.Registry";

        if (IsRuntimeAppPrefix(prefix))
            return "Runtime.App";

        if (prefix.StartsWith("NAV", StringComparison.OrdinalIgnoreCase))
            return "Navigation.Manager";

        if (IsSimulationPrefix(prefix))
            return "Simulation";

        if (IsJobsPrefix(prefix))
            return "Jobs";

        if (IsUiPrefix(prefix))
            return "UI";

        return "App.Legacy";
    }

    private static bool IsContentPrefix(string prefix)
    {
        return prefix.StartsWith("Content", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("RECIPES", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("CONSTR.REG", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRuntimeAppPrefix(string prefix)
    {
        return prefix.StartsWith("STARTUP", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("SHUTDOWN", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("NATIVE", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("HEADLESS", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSimulationPrefix(string prefix)
    {
        return prefix.StartsWith("DIFF", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("EJECT", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("ItemManager", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("CreatureManager", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJobsPrefix(string prefix)
    {
        return prefix.StartsWith("MINING", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("HAUL", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("BUILD", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("CM-", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("ORDERS", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("STOCKPILE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUiPrefix(string prefix)
    {
        return prefix.StartsWith("UI", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("MOUSE", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("KEY", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("RIGHT-CLICK", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("RenderMap", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("FortressState", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("GenerateFortressMap", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("BuildSnapshot", StringComparison.OrdinalIgnoreCase);
    }
}
