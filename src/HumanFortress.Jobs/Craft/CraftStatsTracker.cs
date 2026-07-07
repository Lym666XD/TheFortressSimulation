namespace HumanFortress.Jobs.Craft;

internal sealed class CraftStatsTracker
{
    private int _lastCompleted;
    private int _completedTotal;

    internal CraftJobStatsSnapshot Snapshot { get; private set; }

    internal void RecordRead(int intake, int active, int backlog)
    {
        Snapshot = new CraftJobStatsSnapshot(intake, active, backlog, 0);
    }

    internal void RecordCompleted()
    {
        _completedTotal++;
    }

    internal void RecordWrite(int intake, int active, int backlog)
    {
        Snapshot = new CraftJobStatsSnapshot(intake, active, backlog, _completedTotal - _lastCompleted);
        _lastCompleted = _completedTotal;
    }
}
