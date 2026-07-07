using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

internal sealed partial class OrdersManager
{
    /// <summary>
    /// Enqueue a haul designation for processing.
    /// </summary>
    internal void EnqueueHaul(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        var d = new HaulDesignation(worldRect, z, priority, createdTick);
        _haulQueue.Enqueue(d);
        _recentHauls.Enqueue(d);
        _activeHauls.Add(d);
        while (_recentHauls.Count > RecentCapacity && _recentHauls.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Drain at most maxCount haul designations into the provided list.
    /// Returns number drained. Use in the Read phase.
    /// </summary>
    internal int DrainHaulDesignations(ICollection<HaulDesignation> into, int maxCount)
    {
        var drained = 0;
        while (drained < maxCount && _haulQueue.TryDequeue(out var desig))
        {
            into.Add(desig);
            drained++;
        }

        return drained;
    }

    /// <summary>
    /// Get a snapshot of recently created haul designations (for UI/debug).
    /// </summary>
    internal List<HaulDesignation> GetRecentHauls()
    {
        return _recentHauls.ToList();
    }

    /// <summary>
    /// Get snapshot of active haul designations (persistent until manually cleared).
    /// </summary>
    internal List<HaulDesignation> GetActiveHaulsSnapshot()
    {
        return _activeHauls.ToList();
    }
}
