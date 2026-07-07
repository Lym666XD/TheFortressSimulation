namespace HumanFortress.App.Diagnostics;

internal sealed partial class CategoryRoutingDiagnosticSink
{
    private static string ResolveBucket(string category, string message)
    {
        if (StartsWithAny(category, "Content", "Recipe", "Registry", "Material", "Geology", "ConstructionRegistry"))
        {
            return "content";
        }

        if (StartsWithAny(category, "Runtime", "Startup", "Native", "Headless", "Command", "Tick"))
        {
            return "runtime";
        }

        if (StartsWithAny(category, "Navigation", "NAV", "Path"))
        {
            return "navigation";
        }

        if (StartsWithAny(category, "Jobs", "Mining", "Haul", "Transport", "Construction", "Craft", "CM-PLAN", "CM-DIAG"))
        {
            return "jobs";
        }

        if (StartsWithAny(category, "Simulation", "Items", "Item", "Creature", "Creatures", "Diff", "Orders", "Stockpile", "World"))
        {
            return "simulation";
        }

        if (StartsWithAny(category, "UI", "Render", "FortressState", "EmbarkPrep", "BuildSnapshot", "GenerateFortressMap", "Mouse", "Key"))
        {
            return "ui";
        }

        if (StartsWithAny(category, "Core", "EventBus"))
        {
            return "core";
        }

        return ResolveBucketFromMessage(message);
    }
}
