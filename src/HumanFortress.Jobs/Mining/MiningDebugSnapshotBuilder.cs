namespace HumanFortress.Jobs.Mining;

internal static class MiningDebugSnapshotBuilder
{
    internal static List<MiningActiveJobView> BuildActiveJobs(IReadOnlyList<ActiveMiningJob> active)
    {
        var list = new List<MiningActiveJobView>(active.Count);
        foreach (var job in active)
        {
            list.Add(new MiningActiveJobView(
                job.WorkerId,
                job.Target,
                job.Z,
                job.Adjacent,
                job.Stage.ToString(),
                job.ProgressTicks,
                job.RequiredTicks));
        }

        return list;
    }

    internal static MiningDebugSnapshot BuildDebugSnapshot(
        IReadOnlyList<ActiveMiningJob> active,
        MiningJobStatsSnapshot stats,
        int backlogCount,
        int deferredCount,
        int reservedTileCount,
        int maxActive,
        bool includeSeeds)
    {
        var activeDebug = new List<MiningActiveJobDebugView>(Math.Min(maxActive, active.Count));
        for (int i = 0; i < active.Count && activeDebug.Count < maxActive; i++)
        {
            var job = active[i];
            uint seed = includeSeeds ? MiningPathSeed.From(job.WorkerId, job.Target) : 0u;
            activeDebug.Add(new MiningActiveJobDebugView(
                job.WorkerId,
                job.Target,
                job.Z,
                job.Adjacent,
                job.Stage.ToString(),
                job.ProgressTicks,
                job.RequiredTicks,
                seed));
        }

        return new MiningDebugSnapshot(
            stats,
            activeDebug,
            BacklogCount: backlogCount,
            DeferredCount: deferredCount,
            ReservedTiles: reservedTileCount,
            SeedsIncluded: includeSeeds);
    }
}
