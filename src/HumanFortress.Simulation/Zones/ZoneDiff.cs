using SadRogue.Primitives;

namespace HumanFortress.Simulation.Zones;

internal enum ZoneDiffOp
{
    CreateZone = 1,
    AddCells = 2,
    RemoveCells = 3,
    DeleteZone = 4
}

internal readonly record struct ZoneDiff
{
    internal ZoneDiffOp Op { get; init; }
    internal string DefId { get; init; }
    internal string Name { get; init; }
    internal Rectangle WorldRect { get; init; }
    internal int Z { get; init; }
    internal int ZoneId { get; init; }
    internal ulong CreatedTick { get; init; }
    internal int Priority { get; init; }
    internal string SystemId { get; init; }
    internal int LocalSeq { get; init; }

    internal long GetSortKey()
    {
        return LocalSeq;
    }
}
