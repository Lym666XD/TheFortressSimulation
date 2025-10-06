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
    // Advanced mining
    private readonly ConcurrentQueue<MiningAdvancedDesignation> _miningAdvQueue = new();
    // Construction orders
    private readonly ConcurrentQueue<ConstructionDesignation> _constructionQueue = new();
    private readonly ConcurrentQueue<ConstructionDesignation> _recentConstruction = new();
    private readonly ConcurrentBag<ConstructionDesignation> _activeConstruction = new();

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
    /// Enqueue advanced mining order. DIG/DIG_RAMP decomposed into per-Z MiningDesignation immediately.
    /// Others queued for future handling.
    /// </summary>
    public void EnqueueMiningAdvanced(Rectangle worldRect, int zMin, int zMax, MiningAction action, int priority, ulong createdTick)
    {
        _miningAdvQueue.Enqueue(new MiningAdvancedDesignation(worldRect, zMin, zMax, action, priority, createdTick));
    }

    /// <summary>
    /// Enqueue a construction designation (may span multiple Z).
    /// </summary>
    public void EnqueueConstruction(Rectangle worldRect, int zMin, int zMax, ConstructionShape shape, MaterialFilterSpec filter, int priority, ulong createdTick)
    {
        var d = new ConstructionDesignation(worldRect, zMin, zMax, shape, filter, priority, createdTick);
        _constructionQueue.Enqueue(d);
        _recentConstruction.Enqueue(d);
        _activeConstruction.Add(d);
        while (_recentConstruction.Count > RecentCapacity && _recentConstruction.TryDequeue(out _)) { }
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

    /// <summary>
    /// Drain advanced mining designations into provided list (Read phase).
    /// </summary>
    public int DrainMiningAdvanced(ICollection<MiningAdvancedDesignation> into, int maxCount)
    {
        int drained = 0;
        while (drained < maxCount && _miningAdvQueue.TryDequeue(out var adv))
        {
            into.Add(adv);
            drained++;
        }
        return drained;
    }

    /// <summary>
    /// Drain construction designations into provided list (Read phase).
    /// </summary>
    public int DrainConstructionDesignations(ICollection<ConstructionDesignation> into, int maxCount)
    {
        int drained = 0;
        while (drained < maxCount && _constructionQueue.TryDequeue(out var desig))
        {
            into.Add(desig);
            drained++;
        }
        return drained;
    }

    public List<ConstructionDesignation> GetRecentConstruction() => _recentConstruction.ToList();
    public List<ConstructionDesignation> GetActiveConstructionSnapshot() => _activeConstruction.ToList();
}
