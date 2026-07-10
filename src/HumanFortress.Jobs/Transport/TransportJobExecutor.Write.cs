namespace HumanFortress.Jobs.Transport;

internal sealed partial class TransportJobExecutor
{
    internal void WriteTick(ulong tick)
    {
        if (_active.Count == 0)
        {
            return;
        }

        var finished = new List<ActiveJob>();
        _activeJobRunner.RunWriteTick(_active, tick, finished);
        if (finished.Count > 0)
        {
            foreach (var f in finished)
            {
                _active.Remove(f);
            }

            _statsTracker.RecordFinishedJobs();
        }
    }
}
