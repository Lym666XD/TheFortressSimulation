using SadRogue.Primitives;
using HumanFortress.Simulation.Diff;

namespace HumanFortress.Simulation.Orders;

internal enum OrderDiffOp
{
    Mining = 1,
    AdvancedMining = 2,
    Haul = 3,
    Construction = 4,
    BuildableConstruction = 5
}

internal readonly record struct OrderDiff
{
    internal OrderDiffOp Op { get; init; }
    internal Rectangle WorldRect { get; init; }
    internal Point Anchor { get; init; }
    internal int Z { get; init; }
    internal int ZMin { get; init; }
    internal int ZMax { get; init; }
    internal MiningAction MiningAction { get; init; }
    internal ConstructionShape ConstructionShape { get; init; }
    internal MaterialFilterSpec? MaterialFilter { get; init; }
    internal string ConstructionId { get; init; }
    internal int Priority { get; init; }
    internal ulong CreatedTick { get; init; }
    internal string SystemId { get; init; }
    internal int LocalSeq { get; init; }

    internal long GetSortKey()
    {
        return SimulationDiffSortKeys.ByLocalSequence(LocalSeq);
    }
}
