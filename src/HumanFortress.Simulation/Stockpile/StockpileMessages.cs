using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Message types for cross-chunk stockpile coordination.
/// Per STOCKPILE_SPEC.md section 6.
/// </summary>
internal enum StockpileMessageType
{
    HaulJobAssigned = 1,   // Notify destination chunk
    HaulJobComplete = 2,   // Update source/dest
    HaulJobCancelled = 3,  // Release reservations
    ZoneConfigBatch = 4    // Batch config updates
}

/// <summary>
/// Cross-chunk message for stockpile operations.
/// Sent via Actor mailbox per CONCURRENCY_MODEL.md.
/// </summary>
internal readonly struct StockpileMessage
{
    /// <summary>
    /// Message type.
    /// </summary>
    internal StockpileMessageType Type { get; init; }

    /// <summary>
    /// Zone ID this message relates to.
    /// </summary>
    internal int ZoneId { get; init; }

    /// <summary>
    /// Item handle for haul operations.
    /// </summary>
    internal int ItemHandle { get; init; }

    /// <summary>
    /// Quantity being hauled.
    /// </summary>
    internal int Quantity { get; init; }

    /// <summary>
    /// Source chunk for the operation.
    /// </summary>
    internal ChunkKey SourceChunk { get; init; }

    /// <summary>
    /// Destination chunk for the operation.
    /// </summary>
    internal ChunkKey DestChunk { get; init; }

    /// <summary>
    /// Cell index at destination.
    /// </summary>
    internal int CellIndex { get; init; }

    /// <summary>
    /// Job ID for tracking.
    /// </summary>
    internal int JobId { get; init; }

    /// <summary>
    /// Local sequence for deterministic ordering.
    /// </summary>
    internal int LocalSeq { get; init; }

    /// <summary>
    /// Create a haul job assigned message.
    /// </summary>
    internal static StockpileMessage HaulJobAssigned(
        int jobId,
        int zoneId,
        int itemHandle,
        int quantity,
        ChunkKey sourceChunk,
        ChunkKey destChunk,
        int cellIndex,
        int localSeq)
    {
        return new StockpileMessage
        {
            Type = StockpileMessageType.HaulJobAssigned,
            JobId = jobId,
            ZoneId = zoneId,
            ItemHandle = itemHandle,
            Quantity = quantity,
            SourceChunk = sourceChunk,
            DestChunk = destChunk,
            CellIndex = cellIndex,
            LocalSeq = localSeq
        };
    }

    /// <summary>
    /// Create a haul job complete message.
    /// </summary>
    internal static StockpileMessage HaulJobComplete(
        int jobId,
        int zoneId,
        int itemHandle,
        ChunkKey sourceChunk,
        ChunkKey destChunk,
        int localSeq)
    {
        return new StockpileMessage
        {
            Type = StockpileMessageType.HaulJobComplete,
            JobId = jobId,
            ZoneId = zoneId,
            ItemHandle = itemHandle,
            SourceChunk = sourceChunk,
            DestChunk = destChunk,
            LocalSeq = localSeq
        };
    }

    /// <summary>
    /// Create a haul job cancelled message.
    /// </summary>
    internal static StockpileMessage HaulJobCancelled(
        int jobId,
        int zoneId,
        ChunkKey sourceChunk,
        ChunkKey destChunk,
        int localSeq)
    {
        return new StockpileMessage
        {
            Type = StockpileMessageType.HaulJobCancelled,
            JobId = jobId,
            ZoneId = zoneId,
            SourceChunk = sourceChunk,
            DestChunk = destChunk,
            LocalSeq = localSeq
        };
    }

    /// <summary>
    /// Get deterministic sort key for mailbox draining.
    /// Per CONCURRENCY_MODEL: tick → sourceChunk.Hash → localSeq
    /// </summary>
    internal long GetDrainSortKey(ulong tick)
    {
        // Pack: [tick:32][chunkHash:16][localSeq:16]
        long key = 0;
        key |= ((long)(tick & 0xFFFFFFFF)) << 32;
        key |= ((long)(SourceChunk.GetHashCode() & 0xFFFF)) << 16;
        key |= ((long)(LocalSeq & 0xFFFF));
        return key;
    }
}
