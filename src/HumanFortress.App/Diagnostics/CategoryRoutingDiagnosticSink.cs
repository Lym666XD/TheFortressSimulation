using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App.Diagnostics;

internal sealed class CategoryRoutingDiagnosticSink : IDiagnosticSink, IDisposable
{
    private readonly FileDiagnosticSink _mainSink;
    private readonly Dictionary<string, FileDiagnosticSink> _categorySinks;

    public CategoryRoutingDiagnosticSink(string mainLogPath, string categoryLogDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mainLogPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryLogDirectory);

        Directory.CreateDirectory(categoryLogDirectory);
        _mainSink = new FileDiagnosticSink(mainLogPath);
        _categorySinks = new Dictionary<string, FileDiagnosticSink>(StringComparer.OrdinalIgnoreCase)
        {
            ["app"] = new(Path.Combine(categoryLogDirectory, "app.log")),
            ["content"] = new(Path.Combine(categoryLogDirectory, "content.log")),
            ["core"] = new(Path.Combine(categoryLogDirectory, "core.log")),
            ["jobs"] = new(Path.Combine(categoryLogDirectory, "jobs.log")),
            ["navigation"] = new(Path.Combine(categoryLogDirectory, "navigation.log")),
            ["runtime"] = new(Path.Combine(categoryLogDirectory, "runtime.log")),
            ["simulation"] = new(Path.Combine(categoryLogDirectory, "simulation.log")),
            ["ui"] = new(Path.Combine(categoryLogDirectory, "ui.log"))
        };
    }

    public void Write(DiagnosticEvent diagnosticEvent)
    {
        _mainSink.Write(diagnosticEvent);

        var bucket = ResolveBucket(diagnosticEvent.Category, diagnosticEvent.Message);
        if (_categorySinks.TryGetValue(bucket, out var sink))
        {
            sink.Write(diagnosticEvent);
        }
    }

    public void Dispose()
    {
        _mainSink.Dispose();
        foreach (var sink in _categorySinks.Values)
        {
            sink.Dispose();
        }
    }

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

    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
