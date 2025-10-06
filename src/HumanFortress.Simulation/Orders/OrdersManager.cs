using System.Collections.Concurrent;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Central store for player orders (designations), starting with Haul.
/// Thread-safe for enqueue/dequeue across UI thread and sim thread.
/// </summary>
public sealed class OrdersManager
{
    public static System.Action<string>? LogCallback;
    private readonly ConcurrentQueue<HaulDesignation> _haulQueue = new();
    private readonly ConcurrentQueue<HaulDesignation> _recentHauls = new();
    private const int RecentCapacity = 32;
    private readonly ConcurrentBag<HaulDesignation> _activeHauls = new();
    // Unified mining snapshots and planner queues
    private readonly ConcurrentQueue<MiningDesignation> _recentMining = new();
    private readonly ConcurrentBag<MiningDesignation> _activeMining = new();
    private readonly ConcurrentQueue<MiningDesignation> _miningAdd = new();
    private readonly ConcurrentQueue<MiningCancelRegion> _miningCancel = new();
    private int _nextMiningId = 0;
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
    /// Legacy wrapper kept for compatibility: simple mining becomes Advanced DIG at single Z.
    /// </summary>
    public void EnqueueMining(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        EnqueueMiningAdvanced(worldRect, z, z, MiningAction.Dig, priority, createdTick);
    }

    /// <summary>
    /// Enqueue advanced mining order. DIG/DIG_RAMP decomposed into per-Z MiningDesignation immediately.
    /// Others queued for future handling.
    /// </summary>
    public void EnqueueMiningAdvanced(Rectangle worldRect, int zMin, int zMax, MiningAction action, int priority, ulong createdTick)
    {
        // === Z-AXIS INVERSION FOR STAIRWELLS ===
        // Game internal: Z increases upward (z=25 is ground, z=32 is higher)
        // Player perception: Higher Z values = deeper underground
        //
        // When player scrolls "up" from z=25 to z=32, they expect to dig DOWN into the earth.
        // But internally, we're moving UP in Z. So we need to invert the range for stairwells.
        //
        // Example: Player at z=25 (surface) scrolls to z=32 (wants to dig 7 layers deep)
        //   UI input: zMin=25, zMax=32 (scroll direction: up)
        //   Converted: zMin=18, zMax=25 (actual digging: down into lower Z values)
        //
        // TODO: Long-term fix should align game Z-axis with player perception

        int actualZMin = zMin;
        int actualZMax = zMax;

        if (action == MiningAction.DigStairwell && zMax > zMin)
        {
            int startZ = zMin;  // Player's starting position (e.g., z=25 surface)
            int layerCount = zMax - zMin;  // How many layers player selected (e.g., 7 layers)

            // Invert: dig DOWN from starting point (decrease Z values)
            actualZMin = System.Math.Max(0, startZ - layerCount);  // e.g., 25-7=18 (deepest point)
            actualZMax = startZ;  // e.g., 25 (surface, starting point)

            var _msgConvert = $"[ORDERS] Stairwell Z-inversion: UI z={zMin}..{zMax} ({layerCount} layers) → actual dig z={actualZMin}..{actualZMax} (down from surface)";
            if (LogCallback != null) LogCallback(_msgConvert); else System.Console.WriteLine(_msgConvert);
        }

        var msg = $"[ORDERS] MiningAdvanced enqueued action={action} rect=({worldRect.X},{worldRect.Y},{worldRect.Width}x{worldRect.Height}) z={actualZMin}..{actualZMax} pri={priority}";
        if (LogCallback != null) LogCallback(msg); else System.Console.WriteLine(msg);

        // Unified path: either add designation or emit cancellation region
        if (action == MiningAction.RemoveDigging)
        {
            _miningCancel.Enqueue(new MiningCancelRegion(worldRect, actualZMin, actualZMax, MiningCancelKind.AllMining));
        }
        else
        {
            int id = System.Threading.Interlocked.Increment(ref _nextMiningId);
            var d = new MiningDesignation(id, worldRect, actualZMin, actualZMax, action, priority, createdTick);
            _miningAdd.Enqueue(d);
            _recentMining.Enqueue(d);
            _activeMining.Add(d);
            while (_recentMining.Count > RecentCapacity && _recentMining.TryDequeue(out _)) { }
        }
    }

    /// <summary>
    /// Drain new unified mining designations (V2) into provided list.
    /// </summary>
    public int DrainMiningAdds(ICollection<MiningDesignation> into, int maxCount)
    {
        int drained = 0;
        while (drained < maxCount && _miningAdd.TryDequeue(out var d))
        {
            into.Add(d);
            drained++;
        }
        return drained;
    }

    /// <summary>
    /// Drain mining cancellation regions (RemoveDigging) into provided list.
    /// </summary>
    public int DrainMiningCancels(ICollection<MiningCancelRegion> into, int maxCount)
    {
        int drained = 0;
        while (drained < maxCount && _miningCancel.TryDequeue(out var d))
        {
            into.Add(d);
            drained++;
        }
        return drained;
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

    // V2 snapshots for UI/debug
    public List<MiningDesignation> GetRecentMining() => _recentMining.ToList();
    public List<MiningDesignation> GetActiveMiningSnapshot() => _activeMining.ToList();

    // Legacy DrainMiningAdvanced removed (unified V2 in use)

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

    // Unified mining designation contract
    public readonly record struct MiningDesignation(int Id, Rectangle Rect, int ZMin, int ZMax, MiningAction Action, int Priority, ulong CreatedTick);
    public enum MiningCancelKind { AllMining }
    public readonly record struct MiningCancelRegion(Rectangle Rect, int ZMin, int ZMax, MiningCancelKind Kind);
}
