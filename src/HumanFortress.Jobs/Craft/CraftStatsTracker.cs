namespace HumanFortress.Jobs.Craft;

internal sealed class CraftStatsTracker
{
    private int _lastCompleted;
    private int _completedTotal;

    public CraftJobStatsSnapshot Snapshot { get; private set; }

    public void RecordRead(int intake, int active, int backlog)
    {
        Snapshot = new CraftJobStatsSnapshot(intake, active, backlog, 0);
    }

    public void RecordCompleted()
    {
        _completedTotal++;
    }

    public void RecordWrite(int intake, int active, int backlog)
    {
        Snapshot = new CraftJobStatsSnapshot(intake, active, backlog, _completedTotal - _lastCompleted);
        _lastCompleted = _completedTotal;
    }
}
