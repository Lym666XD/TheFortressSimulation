using HumanFortress.Runtime.Composition;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class JobsDebugSnapshotBuilder
{
    private static TransportDebugView? BuildTransportDebug(SimulationRuntimeSystems systems)
    {
        if (!systems.SchedulerTunings.DebugPanel)
            return null;

        var debug = systems.TransportJobs.GetDebugSnapshot(
            maxActive: 8,
            maxRequests: 8,
            includeSeeds: true);

        return new TransportDebugView(
            debug.PendingPeek
                .Select(req => new TransportRequestDebugView(
                    string.IsNullOrWhiteSpace(req.Reason.ToString()) ? "Request" : req.Reason.ToString(),
                    new JobPoint3(req.To.X, req.To.Y, req.ToZ)))
                .ToList(),
            debug.ShardCounts
                .Select(shard => new TransportShardCountView(shard.ShardId, shard.Count))
                .ToList());
    }
}
