using System.Collections.Concurrent;
using HumanFortress.Simulation.Diagnostics;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Central store for player orders (designations), starting with Haul.
/// Thread-safe for enqueue/dequeue across UI thread and sim thread.
/// </summary>
internal sealed partial class OrdersManager
{
    internal static Action<string>? LogCallback { get; set; }

    private const int RecentCapacity = 32;

    private readonly ConcurrentQueue<HaulDesignation> _haulQueue = new();
    private readonly ConcurrentQueue<HaulDesignation> _recentHauls = new();
    private readonly ConcurrentBag<HaulDesignation> _activeHauls = new();

    // Unified mining snapshots and planner queues
    private readonly ConcurrentQueue<MiningDesignation> _recentMining = new();
    private readonly ConcurrentBag<MiningDesignation> _activeMining = new();
    private readonly ConcurrentQueue<MiningDesignation> _miningAdd = new();
    private readonly ConcurrentQueue<MiningCancelRegion> _miningCancel = new();
    private int _nextMiningId;

    // Construction orders
    private readonly ConcurrentQueue<ConstructionDesignation> _constructionQueue = new();
    private readonly ConcurrentQueue<ConstructionDesignation> _recentConstruction = new();
    private readonly ConcurrentBag<ConstructionDesignation> _activeConstruction = new();

    // Buildable constructions (L2 placeables)
    private readonly ConcurrentQueue<BuildableConstructionDesignation> _buildableQueue = new();
    private readonly ConcurrentQueue<BuildableConstructionDesignation> _recentBuildable = new();
    private readonly ConcurrentBag<BuildableConstructionDesignation> _activeBuildable = new();

    private static void Log(string message)
    {
        SimulationDiagnostics.Information(LogCallback, "Simulation.Orders", message);
    }
}
