using HumanFortress.Contracts.Diagnostics;

namespace HumanFortress.App.Diagnostics;

internal sealed partial class AsyncDiagnosticDispatcher
{
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.CompleteAdding();
        _worker.Join(millisecondsTimeout: 2000);

        WriteDroppedWarningIfNeeded();

        if (_target is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _queue.Dispose();
    }

    private void WriteDroppedWarningIfNeeded()
    {
        if (Interlocked.Read(ref _dropped) <= 0)
            return;

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
}
