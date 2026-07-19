namespace HumanFortress.Simulation.Orders;

internal sealed partial class MiningSystem
{
    internal void PrepareSequentialCompatibility(ulong tick)
    {
        _planned.Clear();

        DrainNewDesignations();
        DrainCancellationRegions();

        if (_active.Count == 0)
            return;

        var actives = OrderedActiveDesignations();
        if (actives.Count == 0)
            return;

        int budget = _maxPerTick;
        var perId = new Dictionary<int, int>();
        bool progress;
        do
        {
            progress = false;
            foreach (var activeDesignation in actives)
            {
                if (budget <= 0)
                    break;
                if (activeDesignation.Done)
                    continue;

                var cursor = activeDesignation;
                if (TryNextDigFrom(ref cursor, out var plannedDig))
                {
                    _planned.Add(plannedDig);
                    budget--;
                    progress = true;
                    if (!perId.ContainsKey(activeDesignation.Id))
                        perId[activeDesignation.Id] = 0;
                    perId[activeDesignation.Id]++;
                }

                _active[activeDesignation.Id] = cursor;
            }

            actives = OrderedActiveDesignations();
        } while (budget > 0 && progress && actives.Count > 0);

        LogPlannedCounts(perId);
    }

    internal void ApplySequentialCompatibility(ulong tick)
    {
        if (_planned.Count == 0)
        {
            if ((tick % 60UL) == 0UL)
            {
                Log("[MINING][COMPAT] apply: no planned digs");
            }
            return;
        }

        Log($"[MINING][COMPAT] apply: enqueuing planned digs: {_planned.Count}");
        foreach (var plannedDig in _planned)
        {
            _outbox.Enqueue(plannedDig);
        }
        _planned.Clear();
    }

    private void DrainNewDesignations()
    {
        var adds = new List<OrdersManager.MiningDesignation>();
        int drainedAdds = _orders.DrainMiningAdds(adds, maxCount: 64);
        if (drainedAdds <= 0)
            return;

        Log("[MINING][PLAN] Adds drained: " + drainedAdds);
        foreach (var designation in adds)
        {
            _active[designation.Id] = new ActiveDesignation(
                designation.Id,
                designation.Rect,
                designation.ZMin,
                designation.ZMax,
                designation.Action,
                designation.Priority,
                designation.CreatedTick);
            Log($"[MINING][PLAN] Designation id={designation.Id} action={designation.Action} rect={designation.Rect} z={designation.ZMin}..{designation.ZMax} layers={designation.ZMax - designation.ZMin + 1}");
        }
    }

    private void DrainCancellationRegions()
    {
        var cancellations = new List<OrdersManager.MiningCancelRegion>();
        int drainedCancels = _orders.DrainMiningCancels(cancellations, maxCount: 64);
        if (drainedCancels <= 0)
            return;

        Log("[MINING][PLAN] Cancels drained: " + drainedCancels);
        _cancels.AddRange(cancellations);
    }

    private List<ActiveDesignation> OrderedActiveDesignations()
    {
        return _active
            .Where(static entry => !entry.Value.Done)
            .OrderByDescending(static entry => entry.Value.Priority)
            .ThenBy(static entry => entry.Key)
            .Select(static entry => entry.Value)
            .ToList();
    }

    private void LogPlannedCounts(Dictionary<int, int> perId)
    {
        foreach (var entry in perId)
        {
            Log("[MINING][PLAN] id=" + entry.Key + " planned+=" + entry.Value);
        }
    }

    /// <summary>
    /// Dequeue up to max planned digs for job creation.
    /// </summary>
    internal int DequeuePlannedDigs(int max, IList<PlannedDig> into)
    {
        int n = 0;
        while (n < max && _outbox.TryDequeue(out var plannedDig))
        {
            into.Add(plannedDig);
            n++;
        }
        return n;
    }
}
