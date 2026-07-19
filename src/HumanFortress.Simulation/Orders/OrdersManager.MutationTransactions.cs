namespace HumanFortress.Simulation.Orders;

internal sealed partial class OrdersManager
{
    internal readonly record struct MutationMemento(
        IReadOnlyList<HaulDesignation> HaulQueue,
        IReadOnlyList<HaulDesignation> RecentHauls,
        IReadOnlyList<HaulDesignation> ActiveHauls,
        IReadOnlyList<MiningDesignation> RecentMining,
        IReadOnlyList<MiningDesignation> ActiveMining,
        IReadOnlyList<MiningDesignation> MiningAdd,
        IReadOnlyList<MiningCancelRegion> MiningCancel,
        int NextMiningId,
        IReadOnlyList<ConstructionDesignation> ConstructionQueue,
        IReadOnlyList<ConstructionDesignation> RecentConstruction,
        IReadOnlyList<ConstructionDesignation> ActiveConstruction,
        IReadOnlyList<BuildableConstructionDesignation> BuildableQueue,
        IReadOnlyList<BuildableConstructionDesignation> RecentBuildable,
        IReadOnlyList<BuildableConstructionDesignation> ActiveBuildable);

    internal MutationMemento CaptureMutationMemento()
    {
        lock (_sync)
        {
            return new MutationMemento(
                _haulQueue.ToArray(),
                _recentHauls.ToArray(),
                _activeHauls.ToArray(),
                _recentMining.ToArray(),
                _activeMining.ToArray(),
                _miningAdd.ToArray(),
                _miningCancel.ToArray(),
                _nextMiningId,
                _constructionQueue.ToArray(),
                _recentConstruction.ToArray(),
                _activeConstruction.ToArray(),
                _buildableQueue.ToArray(),
                _recentBuildable.ToArray(),
                _activeBuildable.ToArray());
        }
    }

    internal void RestoreMutationMemento(MutationMemento memento)
    {
        lock (_sync)
        {
            Replace(_haulQueue, memento.HaulQueue);
            Replace(_recentHauls, memento.RecentHauls);
            Replace(_activeHauls, memento.ActiveHauls);
            Replace(_recentMining, memento.RecentMining);
            Replace(_activeMining, memento.ActiveMining);
            Replace(_miningAdd, memento.MiningAdd);
            Replace(_miningCancel, memento.MiningCancel);
            _nextMiningId = memento.NextMiningId;
            Replace(_constructionQueue, memento.ConstructionQueue);
            Replace(_recentConstruction, memento.RecentConstruction);
            Replace(_activeConstruction, memento.ActiveConstruction);
            Replace(_buildableQueue, memento.BuildableQueue);
            Replace(_recentBuildable, memento.RecentBuildable);
            Replace(_activeBuildable, memento.ActiveBuildable);
        }
    }

    private static void Replace<T>(Queue<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Enqueue(value);
    }

    private static void Replace<T>(List<T> target, IEnumerable<T> values)
    {
        target.Clear();
        target.AddRange(values);
    }
}
