namespace HumanFortress.Jobs.Mining;

internal sealed class MiningStatsTracker
{
    private readonly int _carryoverMaxTicks;
    private MiningJobStatsSnapshot _lastStats;

    public MiningStatsTracker(int carryoverMaxTicks)
    {
        _carryoverMaxTicks = carryoverMaxTicks;
    }

    public int LastIntakeCount { get; private set; }

    public void RecordIntake(int intakeCount)
    {
        LastIntakeCount = intakeCount;
    }

    public void Update(
        ulong tick,
        int activeCount,
        MiningBacklogBuffer backlog,
        int deferredCount,
        int reservedTileCount)
    {
        _lastStats = new MiningJobStatsSnapshot(
            Intake: LastIntakeCount,
            Active: activeCount,
            Backlog: backlog.Count,
            Deferred: deferredCount,
            ReservedTiles: reservedTileCount,
            CarryoverOld: backlog.CountOlderThan(tick, _carryoverMaxTicks));
    }

    public MiningJobStatsSnapshot GetSnapshot() => _lastStats;
}
