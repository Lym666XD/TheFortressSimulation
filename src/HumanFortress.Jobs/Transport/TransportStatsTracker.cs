namespace HumanFortress.Jobs.Transport;

internal sealed class TransportStatsTracker
{
    internal readonly record struct Memento(
        int CompletedTotal,
        int RequeuedTotal,
        int NoPathTotal,
        int LastCompletedTotal,
        int LastRequeuedTotal,
        int LastNoPathTotal,
        TransportJobStatsSnapshot LastStats);

    private int _completedTotal;
    private int _requeuedTotal;
    private int _noPathTotal;
    private int _lastCompletedTotal;
    private int _lastRequeuedTotal;
    private int _lastNoPathTotal;
    private TransportJobStatsSnapshot _lastStats;

    internal TransportJobStatsSnapshot LastStats => _lastStats;

    internal Memento CaptureMemento() => new(
        _completedTotal,
        _requeuedTotal,
        _noPathTotal,
        _lastCompletedTotal,
        _lastRequeuedTotal,
        _lastNoPathTotal,
        _lastStats);

    internal void RestoreMemento(Memento memento)
    {
        _completedTotal = memento.CompletedTotal;
        _requeuedTotal = memento.RequeuedTotal;
        _noPathTotal = memento.NoPathTotal;
        _lastCompletedTotal = memento.LastCompletedTotal;
        _lastRequeuedTotal = memento.LastRequeuedTotal;
        _lastNoPathTotal = memento.LastNoPathTotal;
        _lastStats = memento.LastStats;
    }

    internal void RecordRead(int intake, int active, int backlog, int carryoverOld)
    {
        _lastStats = new TransportJobStatsSnapshot(
            intake,
            active,
            backlog,
            CompletedDelta: _completedTotal - _lastCompletedTotal,
            RequeuedDelta: _requeuedTotal - _lastRequeuedTotal,
            NoPathDelta: _noPathTotal - _lastNoPathTotal,
            CarryoverOld: carryoverOld);
    }

    internal void RecordFinishedJobs()
    {
        _lastCompletedTotal = _completedTotal;
        _lastNoPathTotal = _noPathTotal;
        _lastRequeuedTotal = _requeuedTotal;
    }

    internal void RecordCompleted()
    {
        _completedTotal++;
    }

    internal void RecordNoPath()
    {
        _noPathTotal++;
    }

    internal void RecordRequeued()
    {
        _requeuedTotal++;
    }

    internal void RecordRequeued(int count)
    {
        if (count > 0)
        {
            _requeuedTotal += count;
        }
    }
}
