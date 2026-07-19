using HumanFortress.Simulation.Diff;

namespace HumanFortress.Simulation.Placeables;

internal enum WorkshopDiffOp
{
    AddRecipe = 1,
    RemoveEntry = 2,
    MoveEntry = 3,
    ClearQueue = 4,
    SetWorkerSlots = 5,
    SetAutoStockpile = 6,
    SetAutoSupply = 7
}

internal readonly record struct WorkshopDiff
{
    internal WorkshopDiffOp Op { get; init; }
    internal Guid WorkshopGuid { get; init; }
    internal string RecipeId { get; init; }
    internal Guid? EntryId { get; init; }
    internal int IntValue { get; init; }
    internal int MoveOffset { get; init; }
    internal bool? BoolValue { get; init; }
    internal ulong CurrentTick { get; init; }
    internal int Priority { get; init; }
    internal string SystemId { get; init; }
    internal int LocalSeq { get; init; }

    internal long GetSortKey()
    {
        return SimulationDiffSortKeys.ByLocalSequence(LocalSeq);
    }
}
