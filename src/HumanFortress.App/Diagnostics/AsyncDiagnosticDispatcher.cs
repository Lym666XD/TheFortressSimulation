using System.Collections.Concurrent;
using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App.Diagnostics;

internal sealed class AsyncDiagnosticDispatcher : IDiagnosticSink, IDisposable
{
    private readonly BlockingCollection<DiagnosticEvent> _queue;
    private readonly IDiagnosticSink _target;
    private readonly Thread _worker;
    private long _sequence;
    private long _dropped;
    private bool _disposed;

    public AsyncDiagnosticDispatcher(IDiagnosticSink target, int capacity = 8192)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        }

        _target = target;
        _queue = new BlockingCollection<DiagnosticEvent>(capacity);
        _worker = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "HumanFortress.Diagnostics"
        };
        _worker.Start();
    }

    public long DroppedCount => Interlocked.Read(ref _dropped);

    public void Write(DiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);
        if (_disposed || _queue.IsAddingCompleted)
        {
            return;
        }

        var sequenced = diagnosticEvent.WithSequence(Interlocked.Increment(ref _sequence));
        if (_queue.TryAdd(sequenced))
        {
            return;
        }

        if (diagnosticEvent.Level <= DiagnosticLevel.Debug)
        {
            Interlocked.Increment(ref _dropped);
            return;
        }

        if (!_queue.TryAdd(sequenced, millisecondsTimeout: 50))
        {
            Interlocked.Increment(ref _dropped);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.CompleteAdding();
        _worker.Join(millisecondsTimeout: 2000);

        if (Interlocked.Read(ref _dropped) > 0)
        {
            try
            {
                _target.Write(DiagnosticEvent.Create(
                    DiagnosticLevel.Warning,
                    "Diagnostics",
                    $"Dropped {DroppedCount} diagnostic events because the queue was full.")
                    .WithSequence(Interlocked.Increment(ref _sequence)));
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (_target is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _queue.Dispose();
    }

    private void ProcessQueue()
    {
        try
        {
            foreach (var diagnosticEvent in _queue.GetConsumingEnumerable())
            {
                _target.Write(diagnosticEvent);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
