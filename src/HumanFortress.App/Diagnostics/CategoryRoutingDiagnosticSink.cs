using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App.Diagnostics;

internal sealed partial class CategoryRoutingDiagnosticSink : IDiagnosticSink, IDisposable
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

}
