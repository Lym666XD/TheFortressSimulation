using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App.Diagnostics;

internal sealed class FileDiagnosticSink : IDiagnosticSink, IDisposable
{
    private readonly object _lock = new();
    private readonly StreamWriter _writer;

    public FileDiagnosticSink(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(path, append: false)
        {
            AutoFlush = false
        };
    }

    public void Write(DiagnosticEvent diagnosticEvent)
    {
        lock (_lock)
        {
            _writer.WriteLine(DiagnosticFormatter.Format(diagnosticEvent));
            if (diagnosticEvent.Level >= DiagnosticLevel.Error)
            {
                _writer.Flush();
            }
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer.Flush();
            _writer.Dispose();
        }
    }
}
