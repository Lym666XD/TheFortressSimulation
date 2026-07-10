using HumanFortress.Simulation.Diagnostics;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Central store for player orders (designations), starting with Haul.
/// Simulation-owned order state; ingress and snapshots are guarded so the
/// deterministic command/write phases own ordering instead of concurrent
/// collection enumeration.
/// </summary>
internal sealed partial class OrdersManager
{
    internal static Action<string>? LogCallback { get; set; }

    private const int RecentCapacity = 32;

    private readonly object _sync = new();

    private readonly Queue<HaulDesignation> _haulQueue = new();
    private readonly Queue<HaulDesignation> _recentHauls = new();
    private readonly List<HaulDesignation> _activeHauls = new();

    // Unified mining snapshots and planner queues
    private readonly Queue<MiningDesignation> _recentMining = new();
    private readonly List<MiningDesignation> _activeMining = new();
    private readonly Queue<MiningDesignation> _miningAdd = new();
    private readonly Queue<MiningCancelRegion> _miningCancel = new();
    private int _nextMiningId;

    // Construction orders
    private readonly Queue<ConstructionDesignation> _constructionQueue = new();
    private readonly Queue<ConstructionDesignation> _recentConstruction = new();
    private readonly List<ConstructionDesignation> _activeConstruction = new();

    // Buildable constructions (L2 placeables)
    private readonly Queue<BuildableConstructionDesignation> _buildableQueue = new();
    private readonly Queue<BuildableConstructionDesignation> _recentBuildable = new();
    private readonly List<BuildableConstructionDesignation> _activeBuildable = new();

    private static void Log(string message)
    {
        SimulationDiagnostics.Information(LogCallback, "Simulation.Orders", message);
    }

    private int DrainQueue<T>(Queue<T> queue, ICollection<T> into, int maxCount)
    {
        if (maxCount <= 0)
            return 0;

        lock (_sync)
        {
            var drained = 0;
            while (drained < maxCount && queue.TryDequeue(out var item))
            {
                into.Add(item);
                drained++;
            }

            return drained;
        }
    }

    private static void TrimRecentQueue<T>(Queue<T> queue)
    {
        while (queue.Count > RecentCapacity)
        {
            queue.Dequeue();
        }
    }

    private static void ClearQueue<T>(Queue<T> queue)
    {
        queue.Clear();
    }

    private static void ClearList<T>(List<T> list)
    {
        list.Clear();
    }

    private static IEnumerable<HaulDesignation> OrderHauls(IEnumerable<HaulDesignation> designations)
    {
        return designations
            .OrderBy(static designation => designation.Z)
            .ThenBy(static designation => designation.Priority)
            .ThenBy(static designation => designation.WorldRect.X)
            .ThenBy(static designation => designation.WorldRect.Y)
            .ThenBy(static designation => designation.WorldRect.Width)
            .ThenBy(static designation => designation.WorldRect.Height)
            .ThenBy(static designation => designation.CreatedTick);
    }

    private static IEnumerable<MiningDesignation> OrderMining(IEnumerable<MiningDesignation> designations)
    {
        return designations
            .OrderBy(static designation => designation.Id)
            .ThenBy(static designation => designation.ZMin)
            .ThenBy(static designation => designation.ZMax)
            .ThenBy(static designation => designation.Priority)
            .ThenBy(static designation => designation.Rect.X)
            .ThenBy(static designation => designation.Rect.Y)
            .ThenBy(static designation => designation.Rect.Width)
            .ThenBy(static designation => designation.Rect.Height)
            .ThenBy(static designation => designation.CreatedTick);
    }

    private static IEnumerable<ConstructionDesignation> OrderConstruction(
        IEnumerable<ConstructionDesignation> designations)
    {
        return designations
            .OrderBy(static designation => designation.ZMin)
            .ThenBy(static designation => designation.ZMax)
            .ThenBy(static designation => designation.Priority)
            .ThenBy(static designation => designation.WorldRect.X)
            .ThenBy(static designation => designation.WorldRect.Y)
            .ThenBy(static designation => designation.WorldRect.Width)
            .ThenBy(static designation => designation.WorldRect.Height)
            .ThenBy(static designation => designation.Shape)
            .ThenBy(static designation => designation.Filter.CategoryKey, StringComparer.Ordinal)
            .ThenBy(static designation => designation.Filter.PreferredMaterialId, StringComparer.Ordinal)
            .ThenBy(static designation => BuildMaterialFilterSortKey(designation.Filter), StringComparer.Ordinal)
            .ThenBy(static designation => designation.CreatedTick);
    }

    private static IEnumerable<BuildableConstructionDesignation> OrderBuildable(
        IEnumerable<BuildableConstructionDesignation> designations)
    {
        return designations
            .OrderBy(static designation => designation.ConstructionId, StringComparer.Ordinal)
            .ThenBy(static designation => designation.Anchor.X)
            .ThenBy(static designation => designation.Anchor.Y)
            .ThenBy(static designation => designation.Z)
            .ThenBy(static designation => designation.Priority)
            .ThenBy(static designation => designation.CreatedTick);
    }

    private static string BuildMaterialFilterSortKey(MaterialFilterSpec filter)
    {
        return string.Join('\0', filter.Tags.Order(StringComparer.Ordinal));
    }
}
