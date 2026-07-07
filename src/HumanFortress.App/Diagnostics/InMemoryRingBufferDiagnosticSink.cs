using HumanFortress.Contracts.Diagnostics;

namespace HumanFortress.App.Diagnostics;

internal sealed class InMemoryRingBufferDiagnosticSink : IDiagnosticSink
{
    private readonly object _lock = new();
    private readonly DiagnosticEvent[] _events;
    private int _next;
    private int _count;

    public InMemoryRingBufferDiagnosticSink(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        }

        _events = new DiagnosticEvent[capacity];
    }

    public void Write(DiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);
        lock (_lock)
        {
            _events[_next] = diagnosticEvent;
            _next = (_next + 1) % _events.Length;
            if (_count < _events.Length)
            {
                _count++;
            }
        }
    }

    public IReadOnlyList<DiagnosticEvent> Snapshot()
    {
        lock (_lock)
        {
            var result = new List<DiagnosticEvent>(_count);
            for (var i = 0; i < _count; i++)
            {
                var index = (_next - _count + i + _events.Length) % _events.Length;
                result.Add(_events[index]);
            }

            return result;
        }
    }
}
