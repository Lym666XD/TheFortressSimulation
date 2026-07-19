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
        lock (_sync)
        {
            _constructionQueue.Enqueue(d);
            _recentConstruction.Enqueue(d);
            _activeConstruction.Add(d);
            TrimRecentQueue(_recentConstruction);
        }
    }

    /// <summary>
    /// Enqueue an L2 buildable construction (e.g., workshop) at an anchor cell.
    /// </summary>
    internal void EnqueueBuildableConstruction(string constructionId, Point anchor, int z, int priority, ulong createdTick)
    {
        var d = new BuildableConstructionDesignation(constructionId, anchor, z, priority, createdTick);
        lock (_sync)
        {
            _buildableQueue.Enqueue(d);
            _recentBuildable.Enqueue(d);
            _activeBuildable.Add(d);
            TrimRecentQueue(_recentBuildable);
        }
    }

    /// <summary>
    /// Drain construction designations from serialized compatibility or commit.
    /// </summary>
    internal int DrainConstructionDesignations(ICollection<ConstructionDesignation> into, int maxCount)
    {
        return DrainQueue(_constructionQueue, into, maxCount);
    }

    /// <summary>
    /// Drain buildable construction designations (L2) into provided list.
    /// </summary>
    internal int DrainBuildableConstructions(ICollection<BuildableConstructionDesignation> into, int maxCount)
    {
        return DrainQueue(_buildableQueue, into, maxCount);
    }

    internal List<ConstructionDesignation> GetRecentConstruction()
    {
        lock (_sync)
        {
            return _recentConstruction.ToList();
        }
    }

    internal List<ConstructionDesignation> GetActiveConstructionSnapshot()
    {
        lock (_sync)
        {
            return OrderConstruction(_activeConstruction).ToList();
        }
    }

    internal List<BuildableConstructionDesignation> GetRecentBuildable()
    {
        lock (_sync)
        {
            return _recentBuildable.ToList();
        }
    }

    internal List<BuildableConstructionDesignation> GetActiveBuildableSnapshot()
    {
        lock (_sync)
        {
            return OrderBuildable(_activeBuildable).ToList();
        }
    }
}
