namespace HumanFortress.App.Diagnostics;

internal sealed partial class CategoryRoutingDiagnosticSink
{
    private static string ResolveBucketFromMessage(string message)
    {
        if (message.Contains("[NAV]", StringComparison.OrdinalIgnoreCase))
        {
            return "navigation";
        }

        if (message.Contains("[RECIPES]", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[CONSTR.REG]", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[ContentRegistry]", StringComparison.OrdinalIgnoreCase))
        {
            return "content";
        }

        if (message.Contains("[DIFF]", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[ItemManager]", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[CreatureManager]", StringComparison.OrdinalIgnoreCase))
        {
            return "simulation";
        }

        if (message.Contains("[MINING]", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[HAUL]", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[BUILD.UI]", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[CM-", StringComparison.OrdinalIgnoreCase))
        {
            return "jobs";
        }

        if (message.Contains("[UI]", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[MOUSE]", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[KEY]", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[RenderMap]", StringComparison.OrdinalIgnoreCase))
        {
            return "ui";
        }

        return "app";
    }
}
