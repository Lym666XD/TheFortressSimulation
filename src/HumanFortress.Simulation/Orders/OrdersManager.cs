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
    // Mining orders
    private readonly ConcurrentQueue<MiningDesignation> _miningQueue = new();
    private readonly ConcurrentQueue<MiningDesignation> _recentMining = new();
    private readonly ConcurrentBag<MiningDesignation> _activeMining = new();

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
    /// Enqueue a mining designation for processing.
    /// </summary>
    public void EnqueueMining(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        var d = new MiningDesignation(worldRect, z, priority, createdTick);
        _miningQueue.Enqueue(d);
        _recentMining.Enqueue(d);
        _activeMining.Add(d);
        while (_recentMining.Count > RecentCapacity && _recentMining.TryDequeue(out _)) { }
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

    /// <summary>
    /// Drain mining designations into provided list (Read phase).
    /// </summary>
    public int DrainMiningDesignations(ICollection<MiningDesignation> into, int maxCount)
    {
        int drained = 0;
        while (drained < maxCount && _miningQueue.TryDequeue(out var desig))
        {
            into.Add(desig);
            drained++;
        }
        return drained;
    }

    public List<MiningDesignation> GetRecentMining()
    {
        return _recentMining.ToList();
    }

    public List<MiningDesignation> GetActiveMiningSnapshot()
    {
        return _activeMining.ToList();
    }
}
