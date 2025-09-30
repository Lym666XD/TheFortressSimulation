using System.Collections.Concurrent;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Central store for player orders (designations), starting with Haul.
/// Thread-safe for enqueue/dequeue across UI thread and sim thread.
/// </summary>
public sealed class OrdersManager
{
    private readonly ConcurrentQueue<HaulDesignation> _haulQueue = new();
    private readonly ConcurrentQueue<HaulDesignation> _recentHauls = new();
    private const int RecentCapacity = 32;
    private readonly ConcurrentBag<HaulDesignation> _activeHauls = new();

    /// <summary>
    /// Enqueue a haul designation for processing.
    /// </summary>
    public void EnqueueHaul(Rectangle worldRect, int z, int priority, ulong createdTick)
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
    public int DrainHaulDesignations(ICollection<HaulDesignation> into, int maxCount)
    {
        int drained = 0;
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
    public List<HaulDesignation> GetRecentHauls()
    {
        return _recentHauls.ToList();
    }

    /// <summary>
    /// Get snapshot of active haul designations (persistent until manually cleared).
    /// </summary>
    public List<HaulDesignation> GetActiveHaulsSnapshot()
    {
        return _activeHauls.ToList();
    }
}
