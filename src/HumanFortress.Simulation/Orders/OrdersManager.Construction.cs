using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

internal sealed partial class OrdersManager
{
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
    /// Drain construction designations into provided list (Read phase).
    /// </summary>
    internal int DrainConstructionDesignations(ICollection<ConstructionDesignation> into, int maxCount)
    {
        var drained = 0;
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
        var drained = 0;
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
}
