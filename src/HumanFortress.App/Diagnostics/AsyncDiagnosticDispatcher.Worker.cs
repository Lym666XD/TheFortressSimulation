namespace HumanFortress.App.Diagnostics;

internal sealed partial class AsyncDiagnosticDispatcher
{
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
