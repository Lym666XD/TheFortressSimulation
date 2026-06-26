namespace HumanFortress.Jobs.Mining;

internal sealed class MiningStatsTracker
{
    private readonly int _carryoverMaxTicks;
    private MiningJobStatsSnapshot _lastStats;

    internal MiningStatsTracker(int carryoverMaxTicks)
    {
        _carryoverMaxTicks = carryoverMaxTicks;
    }

    internal int LastIntakeCount { get; private set; }

    internal void RecordIntake(int intakeCount)
    {
        LastIntakeCount = intakeCount;
    }

    internal void Update(
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

    internal MiningJobStatsSnapshot GetSnapshot() => _lastStats;
}
