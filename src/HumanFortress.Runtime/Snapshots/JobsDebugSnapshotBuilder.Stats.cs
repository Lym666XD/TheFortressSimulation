using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class JobsDebugSnapshotBuilder
{
    private static JobStatsView MapTransportStats(TransportJobStatsSnapshot stats)
    {
        return new JobStatsView(
            Intake: stats.Intake,
            Active: stats.Active,
            Backlog: stats.Backlog,
            CompletedDelta: stats.CompletedDelta,
            RequeuedDelta: stats.RequeuedDelta,
            NoPathDelta: stats.NoPathDelta,
            Deferred: 0,
            ReservedTiles: 0,
            CarryoverOld: stats.CarryoverOld);
    }

    private static JobStatsView MapMiningStats(MiningJobStatsSnapshot stats)
    {
        return new JobStatsView(
            Intake: stats.Intake,
            Active: stats.Active,
            Backlog: stats.Backlog,
            CompletedDelta: 0,
            RequeuedDelta: 0,
            NoPathDelta: 0,
            Deferred: stats.Deferred,
            ReservedTiles: stats.ReservedTiles,
            CarryoverOld: stats.CarryoverOld);
    }

    private static JobStatsView MapCraftStats(CraftJobStatsSnapshot stats)
    {
        return new JobStatsView(
            Intake: stats.Intake,
            Active: stats.Active,
            Backlog: stats.Backlog,
            CompletedDelta: stats.CompletedDelta,
            RequeuedDelta: 0,
            NoPathDelta: 0,
            Deferred: 0,
            ReservedTiles: 0,
            CarryoverOld: 0);
    }
}
