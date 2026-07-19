using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Diff operations for stockpile system per STOCKPILE_SPEC.md.
/// Follows the Diff-Log pattern from CONCURRENCY_MODEL.md.
/// </summary>
internal enum StockpileDiffOp
{
    CreateZone = 1,
    DeleteZone = 2,
    AddCells = 3,
    RemoveCells = 4,
    UpdateFilter = 5,
    ReserveSlot = 7,     // Active hauling planner reservation
    ReleaseSlot = 8,
    PlaceItem = 9,
    RemoveItem = 10
}

/// <summary>
/// Immutable diff entry for stockpile operations.
/// Generated during Read phase, applied during Write phase.
/// </summary>
internal readonly struct StockpileDiff
{
    /// <summary>
    /// Operation to perform.
    /// </summary>
    internal StockpileDiffOp Op { get; init; }

    /// <summary>
    /// Target chunk for this operation.
    /// </summary>
    internal ChunkKey TargetChunk { get; init; }

    /// <summary>
    /// Zone ID for this operation.
    /// </summary>
    internal int ZoneId { get; init; }

    /// <summary>
    /// Cell index within chunk (-1 if N/A).
    /// </summary>
    internal int CellIndex { get; init; }

    /// <summary>
    /// Item entity key (0 if N/A).
    /// </summary>
    internal ulong ItemHandle { get; init; }

    /// <summary>
    /// Quantity for operations involving amounts.
    /// </summary>
    internal int Quantity { get; init; }

    /// <summary>
    /// Priority for merge ordering.
    /// </summary>
    internal int Priority { get; init; }

    /// <summary>
    /// System that generated this diff.
    /// </summary>
    internal string SystemId { get; init; }

    /// <summary>
    /// Local sequence number for stable ordering.
    /// </summary>
    internal int LocalSeq { get; init; }

    /// <summary>
    /// Job ID for reservation tracking.
    /// </summary>
    internal int JobId { get; init; }

    /// <summary>
    /// Additional data for specific operations.
    /// </summary>
    internal object? Data { get; init; }

    internal StockpileDiff(
        StockpileDiffOp op,
        ChunkKey targetChunk,
        int zoneId,
        string systemId,
        int localSeq)
    {
        Op = op;
        TargetChunk = targetChunk;
        ZoneId = zoneId;
        CellIndex = -1;
        ItemHandle = 0;
        Quantity = 0;
        Priority = 0;
        SystemId = systemId;
        LocalSeq = localSeq;
        JobId = 0;
        Data = null;
    }

    /// <summary>
    /// Create a coarse deterministic sort key for diagnostics and broad merge grouping.
    /// Authoritative merge ordering is implemented by <see cref="CompareDeterministic"/>.
    /// </summary>
    internal long GetSortKey()
    {
        return SimulationDiffSortKeys.ByStockpileCellPriorityDescending(
            CellIndex,
            Priority,
            (int)Op,
            ZoneId,
            LocalSeq);
    }

    internal static int CompareDeterministic(StockpileDiff left, StockpileDiff right)
    {
        int result = left.CellIndex.CompareTo(right.CellIndex);
        if (result != 0) return result;

        result = left.Priority.CompareTo(right.Priority);
        if (result != 0) return result;

        result = left.Op.CompareTo(right.Op);
        if (result != 0) return result;

        result = left.ZoneId.CompareTo(right.ZoneId);
        if (result != 0) return result;

        result = left.ItemHandle.CompareTo(right.ItemHandle);
        if (result != 0) return result;

        result = string.Compare(left.SystemId, right.SystemId, StringComparison.Ordinal);
        if (result != 0) return result;

        return left.LocalSeq.CompareTo(right.LocalSeq);
    }
}
