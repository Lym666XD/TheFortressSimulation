using System.Collections.Concurrent;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.Diagnostics;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Central store for player orders (designations), starting with Haul.
/// Thread-safe for enqueue/dequeue across UI thread and sim thread.
/// </summary>
internal sealed class OrdersManager
{
    internal static System.Action<string>? LogCallback { get; set; }
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
    // Buildable constructions (L2 placeables)
    private readonly ConcurrentQueue<BuildableConstructionDesignation> _buildableQueue = new();
    private readonly ConcurrentQueue<BuildableConstructionDesignation> _recentBuildable = new();
    private readonly ConcurrentBag<BuildableConstructionDesignation> _activeBuildable = new();

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
    /// Legacy wrapper kept for compatibility: simple mining becomes Advanced DIG at single Z.
    /// </summary>
    internal void EnqueueMining(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        EnqueueMiningAdvanced(worldRect, z, z, MiningAction.Dig, priority, createdTick);
    }

    /// <summary>
    /// Enqueue advanced mining order. DIG/DIG_RAMP decomposed into per-Z MiningDesignation immediately.
    /// Others queued for future handling.
    /// </summary>
    internal void EnqueueMiningAdvanced(Rectangle worldRect, int zMin, int zMax, MiningAction action, int priority, ulong createdTick)
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
            Log(_msgConvert);
        }

        var msg = $"[ORDERS] MiningAdvanced enqueued action={action} rect=({worldRect.X},{worldRect.Y},{worldRect.Width}x{worldRect.Height}) z={actualZMin}..{actualZMax} pri={priority}";
        Log(msg);

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

    private static void Log(string message)
    {
        SimulationDiagnostics.Information(LogCallback, "Simulation.Orders", message);
    }

    /// <summary>
    /// Drain new unified mining designations (V2) into provided list.
    /// </summary>
    internal int DrainMiningAdds(ICollection<MiningDesignation> into, int maxCount)
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
    internal int DrainMiningCancels(ICollection<MiningCancelRegion> into, int maxCount)
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
    internal void EnqueueConstruction(Rectangle worldRect, int zMin, int zMax, ConstructionShape shape, MaterialFilterSpec filter, int priority, ulong createdTick)
    {
        var d = new ConstructionDesignation(worldRect, zMin, zMax, shape, filter, priority, createdTick);
        _constructionQueue.Enqueue(d);
        _recentConstruction.Enqueue(d);
        _activeConstruction.Add(d);
        while (_recentConstruction.Count > RecentCapacity && _recentConstruction.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Enqueue an L2 buildable construction (e.g., workshop) at an anchor cell.
    /// </summary>
    internal void EnqueueBuildableConstruction(string constructionId, Point anchor, int z, int priority, ulong createdTick)
    {
        var d = new BuildableConstructionDesignation(constructionId, anchor, z, priority, createdTick);
        _buildableQueue.Enqueue(d);
        _recentBuildable.Enqueue(d);
        _activeBuildable.Add(d);
        while (_recentBuildable.Count > RecentCapacity && _recentBuildable.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Drain at most maxCount haul designations into the provided list.
    /// Returns number drained. Use in the Read phase.
    /// </summary>
    internal int DrainHaulDesignations(ICollection<HaulDesignation> into, int maxCount)
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

    // V2 snapshots for UI/debug
    internal List<MiningDesignation> GetRecentMining() => _recentMining.ToList();
    internal List<MiningDesignation> GetActiveMiningSnapshot() => _activeMining.ToList();

    // Legacy DrainMiningAdvanced removed (unified V2 in use)

    /// <summary>
    /// Drain construction designations into provided list (Read phase).
    /// </summary>
    internal int DrainConstructionDesignations(ICollection<ConstructionDesignation> into, int maxCount)
    {
        int drained = 0;
        while (drained < maxCount && _constructionQueue.TryDequeue(out var desig))
        {
            into.Add(desig);
            drained++;
        }
        return drained;
    }

    /// <summary>
    /// Drain buildable construction designations (L2) into provided list.
    /// </summary>
    internal int DrainBuildableConstructions(ICollection<BuildableConstructionDesignation> into, int maxCount)
    {
        int drained = 0;
        while (drained < maxCount && _buildableQueue.TryDequeue(out var d))
        {
            into.Add(d);
            drained++;
        }
        return drained;
    }

    internal List<ConstructionDesignation> GetRecentConstruction() => _recentConstruction.ToList();
    internal List<ConstructionDesignation> GetActiveConstructionSnapshot() => _activeConstruction.ToList();
    internal List<BuildableConstructionDesignation> GetRecentBuildable() => _recentBuildable.ToList();
    internal List<BuildableConstructionDesignation> GetActiveBuildableSnapshot() => _activeBuildable.ToList();

    internal IReadOnlyList<string> RestoreActiveSnapshot(
        IReadOnlyList<WorldSaveMiningOrderPayloadData>? mining,
        IReadOnlyList<WorldSaveHaulOrderPayloadData>? hauls,
        IReadOnlyList<WorldSaveConstructionOrderPayloadData>? construction,
        IReadOnlyList<WorldSaveBuildableOrderPayloadData>? buildable)
    {
        var issues = new List<string>();
        if (mining == null) issues.Add("World mining order payload is missing.");
        if (hauls == null) issues.Add("World haul order payload is missing.");
        if (construction == null) issues.Add("World construction order payload is missing.");
        if (buildable == null) issues.Add("World buildable order payload is missing.");
        if (issues.Count > 0)
            return issues;

        ValidateMiningOrders(mining!, issues);
        ValidateHaulOrders(hauls!, issues);
        ValidateConstructionOrders(construction!, issues);
        ValidateBuildableOrders(buildable!, issues);
        if (issues.Count > 0)
            return issues;

        ClearQueue(_haulQueue);
        ClearQueue(_recentHauls);
        ClearBag(_activeHauls);
        ClearQueue(_recentMining);
        ClearBag(_activeMining);
        ClearQueue(_miningAdd);
        ClearQueue(_miningCancel);
        ClearQueue(_constructionQueue);
        ClearQueue(_recentConstruction);
        ClearBag(_activeConstruction);
        ClearQueue(_buildableQueue);
        ClearQueue(_recentBuildable);
        ClearBag(_activeBuildable);

        var maxMiningId = 0;
        foreach (var payload in mining!.OrderBy(order => order.Id))
        {
            var designation = new MiningDesignation(
                payload.Id,
                ToRectangle(payload.Rect),
                payload.ZMin,
                payload.ZMax,
                (MiningAction)payload.Action,
                payload.Priority,
                payload.CreatedTick);
            _miningAdd.Enqueue(designation);
            _recentMining.Enqueue(designation);
            _activeMining.Add(designation);
            maxMiningId = Math.Max(maxMiningId, payload.Id);
        }

        _nextMiningId = maxMiningId;

        foreach (var payload in hauls!
            .OrderBy(order => order.Z)
            .ThenBy(order => order.Priority)
            .ThenBy(order => order.WorldRect.X)
            .ThenBy(order => order.WorldRect.Y))
        {
            var designation = new HaulDesignation(
                ToRectangle(payload.WorldRect),
                payload.Z,
                payload.Priority,
                payload.CreatedTick);
            _haulQueue.Enqueue(designation);
            _recentHauls.Enqueue(designation);
            _activeHauls.Add(designation);
        }

        foreach (var payload in construction!
            .OrderBy(order => order.ZMin)
            .ThenBy(order => order.ZMax)
            .ThenBy(order => order.Priority)
            .ThenBy(order => order.WorldRect.X)
            .ThenBy(order => order.WorldRect.Y))
        {
            var designation = new ConstructionDesignation(
                ToRectangle(payload.WorldRect),
                payload.ZMin,
                payload.ZMax,
                (ConstructionShape)payload.Shape,
                ToMaterialFilter(payload.Filter),
                payload.Priority,
                payload.CreatedTick);
            _constructionQueue.Enqueue(designation);
            _recentConstruction.Enqueue(designation);
            _activeConstruction.Add(designation);
        }

        foreach (var payload in buildable!
            .OrderBy(order => order.ConstructionId, StringComparer.Ordinal)
            .ThenBy(order => order.Anchor.X)
            .ThenBy(order => order.Anchor.Y)
            .ThenBy(order => order.Z)
            .ThenBy(order => order.Priority))
        {
            var designation = new BuildableConstructionDesignation(
                payload.ConstructionId,
                new Point(payload.Anchor.X, payload.Anchor.Y),
                payload.Z,
                payload.Priority,
                payload.CreatedTick);
            _buildableQueue.Enqueue(designation);
            _recentBuildable.Enqueue(designation);
            _activeBuildable.Add(designation);
        }

        TrimRecentQueues();

        return Array.Empty<string>();
    }

    // Unified mining designation contract
    internal readonly record struct MiningDesignation(int Id, Rectangle Rect, int ZMin, int ZMax, MiningAction Action, int Priority, ulong CreatedTick);
    internal enum MiningCancelKind { AllMining }
    internal readonly record struct MiningCancelRegion(Rectangle Rect, int ZMin, int ZMax, MiningCancelKind Kind);

    private static void ValidateMiningOrders(
        IReadOnlyList<WorldSaveMiningOrderPayloadData> orders,
        ICollection<string> issues)
    {
        var seen = new HashSet<int>();
        for (var i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            var prefix = $"World mining order payload[{i}]";
            if (order.Id <= 0)
            {
                issues.Add($"{prefix} has non-positive id {order.Id}.");
            }
            else if (!seen.Add(order.Id))
            {
                issues.Add($"{prefix} duplicates mining id {order.Id}.");
            }

            ValidateRectangle(order.Rect, prefix, issues);

            if (order.ZMin > order.ZMax)
            {
                issues.Add($"{prefix} has zMin greater than zMax.");
            }

            if (!IsDefinedMiningAction(order.Action))
            {
                issues.Add($"{prefix} has unsupported mining action {order.Action}.");
            }
        }
    }

    private static void ValidateHaulOrders(
        IReadOnlyList<WorldSaveHaulOrderPayloadData> orders,
        ICollection<string> issues)
    {
        for (var i = 0; i < orders.Count; i++)
        {
            ValidateRectangle(orders[i].WorldRect, $"World haul order payload[{i}]", issues);
        }
    }

    private static void ValidateConstructionOrders(
        IReadOnlyList<WorldSaveConstructionOrderPayloadData> orders,
        ICollection<string> issues)
    {
        for (var i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            var prefix = $"World construction order payload[{i}]";
            ValidateRectangle(order.WorldRect, prefix, issues);

            if (order.ZMin > order.ZMax)
            {
                issues.Add($"{prefix} has zMin greater than zMax.");
            }

            if (!IsDefinedConstructionShape(order.Shape))
            {
                issues.Add($"{prefix} has unsupported construction shape {order.Shape}.");
            }

            if (string.IsNullOrWhiteSpace(order.Filter.CategoryKey))
            {
                issues.Add($"{prefix} has a blank material filter category key.");
            }
        }
    }

    private static void ValidateBuildableOrders(
        IReadOnlyList<WorldSaveBuildableOrderPayloadData> orders,
        ICollection<string> issues)
    {
        for (var i = 0; i < orders.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(orders[i].ConstructionId))
            {
                issues.Add($"World buildable order payload[{i}] has a blank construction id.");
            }
        }
    }

    private static void ValidateRectangle(
        WorldSaveRectangleData rectangle,
        string prefix,
        ICollection<string> issues)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            issues.Add($"{prefix} has non-positive rectangle dimensions.");
        }
    }

    private static MaterialFilterSpec ToMaterialFilter(WorldSaveMaterialFilterPayloadData payload)
    {
        return new MaterialFilterSpec
        {
            PreferredMaterialId = payload.PreferredMaterialId,
            CategoryKey = payload.CategoryKey,
            Tags = payload.Tags?
                .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .Order(StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<string>()
        };
    }

    private static Rectangle ToRectangle(WorldSaveRectangleData rectangle)
    {
        return new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }

    private static bool IsDefinedMiningAction(int value)
    {
        return value is >= byte.MinValue and <= byte.MaxValue
            && Enum.IsDefined(typeof(MiningAction), (byte)value);
    }

    private static bool IsDefinedConstructionShape(int value)
    {
        return value is >= byte.MinValue and <= byte.MaxValue
            && Enum.IsDefined(typeof(ConstructionShape), (byte)value);
    }

    private void TrimRecentQueues()
    {
        while (_recentHauls.Count > RecentCapacity && _recentHauls.TryDequeue(out _)) { }
        while (_recentMining.Count > RecentCapacity && _recentMining.TryDequeue(out _)) { }
        while (_recentConstruction.Count > RecentCapacity && _recentConstruction.TryDequeue(out _)) { }
        while (_recentBuildable.Count > RecentCapacity && _recentBuildable.TryDequeue(out _)) { }
    }

    private static void ClearQueue<T>(ConcurrentQueue<T> queue)
    {
        while (queue.TryDequeue(out _)) { }
    }

    private static void ClearBag<T>(ConcurrentBag<T> bag)
    {
        while (bag.TryTake(out _)) { }
    }
}
