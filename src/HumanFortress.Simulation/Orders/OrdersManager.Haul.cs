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
        lock (_sync)
        {
            _haulQueue.Enqueue(d);
            _recentHauls.Enqueue(d);
            _activeHauls.Add(d);
            TrimRecentQueue(_recentHauls);
        }
    }

    /// <summary>
    /// Drain at most maxCount haul designations into the provided list.
    /// Returns number drained. Use only from serialized compatibility or commit.
    /// </summary>
    internal int DrainHaulDesignations(ICollection<HaulDesignation> into, int maxCount)
    {
        return DrainQueue(_haulQueue, into, maxCount);
    }

    /// <summary>
    /// Get a snapshot of recently created haul designations (for UI/debug).
    /// </summary>
    internal List<HaulDesignation> GetRecentHauls()
    {
        lock (_sync)
        {
            return _recentHauls.ToList();
        }
    }

    /// <summary>
    /// Get snapshot of active haul designations (persistent until manually cleared).
    /// </summary>
    internal List<HaulDesignation> GetActiveHaulsSnapshot()
    {
        lock (_sync)
        {
            return OrderHauls(_activeHauls).ToList();
        }
    }
}
