namespace HumanFortress.Jobs.Transport;

internal sealed class TransportStatsTracker
{
    private int _lastCompletedTotal;
    private int _lastRequeuedTotal;
    private int _lastNoPathTotal;
    private TransportJobStatsSnapshot _lastStats;

    public TransportJobStatsSnapshot LastStats => _lastStats;

    public void RecordRead(int intake, int active, int backlog, int carryoverOld)
    {
        _lastStats = new TransportJobStatsSnapshot(
            intake,
            active,
            backlog,
            CompletedDelta: JobStats.Completed - _lastCompletedTotal,
            RequeuedDelta: JobStats.Requeued - _lastRequeuedTotal,
            NoPathDelta: JobStats.NoPath - _lastNoPathTotal,
            CarryoverOld: carryoverOld);
    }

    public void RecordFinishedJobs()
    {
        _lastCompletedTotal = JobStats.Completed;
        _lastNoPathTotal = JobStats.NoPath;
        _lastRequeuedTotal = JobStats.Requeued;
    }
}
