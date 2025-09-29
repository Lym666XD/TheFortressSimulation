using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Diff operations for stockpile system per STOCKPILE_SPEC.md.
/// Follows the Diff-Log pattern from CONCURRENCY_MODEL.md.
/// </summary>
public enum StockpileDiffOp
{
    CreateZone = 1,
    DeleteZone = 2,
    AddCells = 3,
    RemoveCells = 4,
    UpdateFilter = 5,
    CreateHaulJob = 6,   // Broker output
    ReserveSlot = 7,     // Atomic with CreateHaulJob
    ReleaseSlot = 8,
    PlaceItem = 9,
    RemoveItem = 10
}

/// <summary>
/// Immutable diff entry for stockpile operations.
/// Generated during Read phase, applied during Write phase.
/// </summary>
public readonly struct StockpileDiff
{
    /// <summary>
    /// Operation to perform.
    /// </summary>
    public StockpileDiffOp Op { get; init; }

    /// <summary>
    /// Target chunk for this operation.
    /// </summary>
    public ChunkKey TargetChunk { get; init; }

    /// <summary>
    /// Zone ID for this operation.
    /// </summary>
    public int ZoneId { get; init; }

    /// <summary>
    /// Cell index within chunk (-1 if N/A).
    /// </summary>
    public int CellIndex { get; init; }

    /// <summary>
    /// Item handle (0 if N/A).
    /// </summary>
    public int ItemHandle { get; init; }

    /// <summary>
    /// Quantity for operations involving amounts.
    /// </summary>
    public int Quantity { get; init; }

    /// <summary>
    /// Priority for merge ordering.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// System that generated this diff.
    /// </summary>
    public string SystemId { get; init; }

    /// <summary>
    /// Local sequence number for stable ordering.
    /// </summary>
    public int LocalSeq { get; init; }

    /// <summary>
    /// Job ID for reservation tracking.
    /// </summary>
    public int JobId { get; init; }

    /// <summary>
    /// Additional data for specific operations.
    /// </summary>
    public object? Data { get; init; }

    public StockpileDiff(
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
    /// Create a deterministic sort key for merge ordering.
    /// Per STOCKPILE_SPEC: cellIndex → priority(desc) → op → zoneId → itemHandle → systemId → localSeq
    /// </summary>
    public long GetSortKey()
    {
        // Pack into a long for efficient sorting
        // Format: [cellIndex:16][priority:8][op:8][zoneId:16][localSeq:16]
        long key = 0;
        key |= ((long)(CellIndex & 0xFFFF)) << 48;
        key |= ((long)(255 - Priority) & 0xFF) << 40; // Invert for descending
        key |= ((long)Op & 0xFF) << 32;
        key |= ((long)(ZoneId & 0xFFFF)) << 16;
        key |= ((long)(LocalSeq & 0xFFFF));
        return key;
    }
}