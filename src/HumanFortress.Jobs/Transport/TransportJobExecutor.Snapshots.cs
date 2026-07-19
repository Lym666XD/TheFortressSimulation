namespace HumanFortress.Jobs.Transport;

internal sealed partial class TransportJobExecutor
{
    internal HumanFortress.Contracts.Navigation.MovementCursorData? GetMovementCursorSnapshot(
        Guid creatureId) => _move.GetCursorSnapshot(
            HumanFortress.Core.Simulation.DiffTargetEncoding.EntityKey(creatureId));

    internal List<TransportActiveJobView> GetActiveJobsSnapshot()
    {
        var list = new List<TransportActiveJobView>(_active.Count);
        foreach (var j in _active)
        {
            var from = j.Stage == JobStage.ToItem ? GetItemPos(j) : GetCreaturePos(j.CreatureId);
            list.Add(new TransportActiveJobView(j.CreatureId, j.ItemId, from, j.Dest, j.Stage.ToString()));
        }

        return list;
    }

    internal TransportDebugSnapshot GetDebugSnapshot(int maxActive = 8, int maxRequests = 8, bool includeSeeds = false)
    {
        var stats = GetLastStatsSnapshot();
        var active = new List<TransportActiveJobDebugView>(Math.Min(maxActive, _active.Count));
        for (int i = 0; i < _active.Count && active.Count < maxActive; i++)
        {
            var j = _active[i];
            var from = j.Stage == JobStage.ToItem ? GetItemPos(j) : GetCreaturePos(j.CreatureId);
            uint seed = includeSeeds ? SeedFrom(j.CreatureId, j.ItemId) : 0u;
            active.Add(new TransportActiveJobDebugView(j.CreatureId, j.ItemId, from, j.Dest, j.Stage.ToString(), seed));
        }

        var pendingPeek = _requests.Peek(maxRequests).ToList();
        var shards = _requests.GetShardCountsSnapshot()
            .OrderBy(static kv => kv.Key)
            .Select(static kv => new TransportShardCountDebugView(kv.Key, kv.Value))
            .ToArray();
        int workers = _world.Creatures.GetAllInstances().Count();
        int allowedActive = GetAllowedActiveCount(workers);
        int reserved = Math.Min(workers, Math.Max(0, _hintReserveSlots));

        return new TransportDebugSnapshot(
            stats,
            active,
            pendingPeek,
            shards,
            BacklogCount: _backlog.Count,
            IntakeBudget: GetEffectiveIntakeBudget(),
            AllowedActive: allowedActive == int.MaxValue ? -1 : allowedActive,
            ReservedSlots: reserved,
            SeedsIncluded: includeSeeds);
    }

    internal TransportJobReplaySnapshot GetReplaySnapshot()
    {
        var active = new TransportActiveJobStateSnapshot[_active.Count];
        for (var i = 0; i < _active.Count; i++)
        {
            var job = _active[i];
            active[i] = new TransportActiveJobStateSnapshot(
                i,
                job.CreatureId,
                job.ItemId,
                job.Dest,
                job.Stage,
                job.Quantity,
                job.InvalidReplanCount,
                job.Reason,
                job.PathSearchAttempt,
                _move.GetCursorSnapshot(
                    HumanFortress.Core.Simulation.DiffTargetEncoding.EntityKey(job.CreatureId)));
        }

        return new TransportJobReplaySnapshot(
            _hintIntakeCap,
            _hintMaxActive,
            _hintReserveSlots,
            active,
            _backlog.GetStateSnapshot());
    }
}
