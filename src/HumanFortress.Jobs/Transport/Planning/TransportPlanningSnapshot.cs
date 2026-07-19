using System.Collections.Immutable;
using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport.Planning;

internal enum TransportPendingSource : byte
{
    Queue,
    Backlog
}

internal enum TransportReservationResourceKind : byte
{
    Item,
    Creature
}

internal readonly record struct TransportRequestReadRow(
    Guid ItemId,
    Point3 Source,
    Point3 Destination,
    int Quantity,
    TransportReason Reason,
    int Priority,
    ulong CreatedTick,
    int SystemOrder,
    string ProducerId,
    ulong LocalSequence,
    uint Seed,
    byte PathSearchAttempt,
    ImmutableArray<Guid> RankedWorkerIds = default);

internal readonly record struct TransportQueuedRequestReadRow(
    int QueueOrder,
    TransportRequestReadRow Request);

internal readonly record struct TransportBacklogRequestReadRow(
    int BacklogOrder,
    ulong EnqueuedTick,
    TransportRequestReadRow Request);

internal readonly record struct TransportActiveJobReadRow(
    Guid ItemId,
    Guid CreatureId,
    Point3 Destination,
    JobStage Stage,
    int Quantity,
    TransportReason Reason,
    int Priority,
    ulong CreatedTick,
    int SystemOrder,
    string ProducerId,
    ulong LocalSequence,
    int InvalidReplanCount,
    byte PathSearchAttempt,
    Guid PendingSplitItemId = default,
    ulong PendingSplitGeneration = 0,
    ulong PendingSplitIssuedTick = 0)
{
    internal bool HasPendingSplit => PendingSplitItemId != Guid.Empty
        && PendingSplitGeneration != 0;
}

internal readonly record struct TransportItemReadRow(
    Guid ItemId,
    ulong EntityKey,
    string DefinitionId,
    Point3 Position,
    int StackCount,
    bool IsOnGround,
    Guid CarrierId);

internal readonly record struct TransportCreatureReadRow(
    Guid CreatureId,
    ulong EntityKey,
    Point3 Position,
    int HitPoints,
    bool CanHaul);

internal readonly record struct TransportReservationReadRow(
    TransportReservationResourceKind ResourceKind,
    Guid ResourceId,
    Guid HolderId,
    string HolderSystem,
    string JobId,
    ulong Generation,
    ulong ExpireTick,
    bool IsStagedTransfer);

internal readonly record struct TransportStockpileCellReadRow(
    int ZoneId,
    Point3 Position,
    uint Generation,
    ImmutableArray<string> AcceptedDefinitionIds,
    Guid OccupyingItemId)
{
    internal bool Accepts(string definitionId)
    {
        return AcceptedDefinitionIds.IsDefaultOrEmpty
            || AcceptedDefinitionIds.Any(candidate =>
                string.Equals(candidate, definitionId, StringComparison.Ordinal));
    }
}

internal readonly record struct TransportNavigationRevisionReadRow(
    ChunkKey Chunk,
    ulong ConnectivityVersion);

internal readonly record struct TransportMovementCursorReadRow(
    Guid CreatureId,
    ulong EntityKey,
    ulong Revision,
    Point3 Position,
    Point3 Destination,
    uint PathHash,
    int CurrentStep,
    int StepCount,
    int StepWait,
    int StuckTicks,
    int LastProgress,
    ChunkKey CurrentChunk,
    ulong ExpectedConnectivityVersion);

/// <summary>
/// Canonical, immutable authority projection consumed by transport planning.
/// Capturing this projection is the only place that may inspect live owners.
/// </summary>
internal sealed record TransportPlanningSnapshot
{
    private TransportPlanningSnapshot(
        ulong tick,
        ImmutableArray<TransportQueuedRequestReadRow> queuedRequests,
        ImmutableArray<TransportBacklogRequestReadRow> backlogRequests,
        ImmutableArray<TransportActiveJobReadRow> activeJobs,
        ImmutableArray<TransportItemReadRow> items,
        ImmutableArray<TransportCreatureReadRow> creatures,
        ImmutableArray<TransportReservationReadRow> reservations,
        ImmutableArray<TransportStockpileCellReadRow> stockpileCells,
        ImmutableArray<Point3> observedNonStockpileDestinations,
        ImmutableArray<TransportNavigationRevisionReadRow> navigationRevisions,
        ImmutableArray<TransportMovementCursorReadRow> movementCursors)
    {
        Tick = tick;
        QueuedRequests = queuedRequests;
        BacklogRequests = backlogRequests;
        ActiveJobs = activeJobs;
        Items = items;
        Creatures = creatures;
        Reservations = reservations;
        StockpileCells = stockpileCells;
        ObservedNonStockpileDestinations = observedNonStockpileDestinations;
        NavigationRevisions = navigationRevisions;
        MovementCursors = movementCursors;
    }

    internal ulong Tick { get; }
    internal ImmutableArray<TransportQueuedRequestReadRow> QueuedRequests { get; }
    internal ImmutableArray<TransportBacklogRequestReadRow> BacklogRequests { get; }
    internal ImmutableArray<TransportActiveJobReadRow> ActiveJobs { get; }
    internal ImmutableArray<TransportItemReadRow> Items { get; }
    internal ImmutableArray<TransportCreatureReadRow> Creatures { get; }
    internal ImmutableArray<TransportReservationReadRow> Reservations { get; }
    internal ImmutableArray<TransportStockpileCellReadRow> StockpileCells { get; }
    internal ImmutableArray<Point3> ObservedNonStockpileDestinations { get; }
    internal ImmutableArray<TransportNavigationRevisionReadRow> NavigationRevisions { get; }
    internal ImmutableArray<TransportMovementCursorReadRow> MovementCursors { get; }

    internal static TransportPlanningSnapshot Create(
        ulong tick,
        IEnumerable<TransportQueuedRequestReadRow>? queuedRequests = null,
        IEnumerable<TransportBacklogRequestReadRow>? backlogRequests = null,
        IEnumerable<TransportActiveJobReadRow>? activeJobs = null,
        IEnumerable<TransportItemReadRow>? items = null,
        IEnumerable<TransportCreatureReadRow>? creatures = null,
        IEnumerable<TransportReservationReadRow>? reservations = null,
        IEnumerable<TransportStockpileCellReadRow>? stockpileCells = null,
        IEnumerable<TransportNavigationRevisionReadRow>? navigationRevisions = null,
        IEnumerable<TransportMovementCursorReadRow>? movementCursors = null,
        IEnumerable<Point3>? observedNonStockpileDestinations = null)
    {
        var canonicalItems = (items ?? Array.Empty<TransportItemReadRow>())
            .OrderBy(static row => row.ItemId)
            .ToImmutableArray();
        var canonicalCreatures = (creatures ?? Array.Empty<TransportCreatureReadRow>())
            .OrderBy(static row => row.CreatureId)
            .ToImmutableArray();
        var canonicalActive = (activeJobs ?? Array.Empty<TransportActiveJobReadRow>())
            .OrderBy(static row => row.ItemId)
            .ThenBy(static row => row.CreatureId)
            .ToImmutableArray();
        var canonicalReservations = (reservations ?? Array.Empty<TransportReservationReadRow>())
            .OrderBy(static row => row.ResourceKind)
            .ThenBy(static row => row.ResourceId)
            .ThenBy(static row => row.Generation)
            .ToImmutableArray();
        var canonicalStockpiles = (stockpileCells ?? Array.Empty<TransportStockpileCellReadRow>())
            .Select(static row => row with
            {
                AcceptedDefinitionIds = row.AcceptedDefinitionIds.IsDefault
                    ? ImmutableArray<string>.Empty
                    : row.AcceptedDefinitionIds
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(static id => id, StringComparer.Ordinal)
                        .ToImmutableArray()
            })
            .OrderBy(static row => row.Position.Z)
            .ThenBy(static row => row.Position.Y)
            .ThenBy(static row => row.Position.X)
            .ThenBy(static row => row.ZoneId)
            .ToImmutableArray();
        var canonicalNonStockpiles = (observedNonStockpileDestinations ?? Array.Empty<Point3>())
            .Distinct()
            .OrderBy(static point => point.Z)
            .ThenBy(static point => point.Y)
            .ThenBy(static point => point.X)
            .ToImmutableArray();
        var canonicalRevisions = (navigationRevisions ?? Array.Empty<TransportNavigationRevisionReadRow>())
            .OrderBy(static row => row.Chunk.Z)
            .ThenBy(static row => row.Chunk.ChunkY)
            .ThenBy(static row => row.Chunk.ChunkX)
            .ToImmutableArray();
        var canonicalCursors = (movementCursors ?? Array.Empty<TransportMovementCursorReadRow>())
            .OrderBy(static row => row.CreatureId)
            .ToImmutableArray();

        EnsureUnique(canonicalItems, static row => row.ItemId, "item");
        EnsureUnique(canonicalCreatures, static row => row.CreatureId, "creature");
        EnsureUnique(canonicalActive, static row => row.ItemId, "active item");
        EnsureUnique(canonicalActive, static row => row.CreatureId, "active creature");
        EnsureUnique(canonicalReservations, static row => (row.ResourceKind, row.ResourceId), "reservation resource");
        EnsureUnique(canonicalStockpiles, static row => row.Position, "stockpile cell");
        EnsureUnique(canonicalRevisions, static row => row.Chunk, "navigation chunk");
        EnsureUnique(canonicalCursors, static row => row.CreatureId, "movement cursor");
        if (canonicalStockpiles.Any(row => canonicalNonStockpiles.Contains(row.Position)))
        {
            throw new InvalidOperationException(
                "Transport planning snapshot cannot observe one destination as both stockpile and non-stockpile.");
        }

        return new TransportPlanningSnapshot(
            tick,
            CanonicalizeQueue(queuedRequests),
            CanonicalizeBacklog(backlogRequests),
            canonicalActive,
            canonicalItems,
            canonicalCreatures,
            canonicalReservations,
            canonicalStockpiles,
            canonicalNonStockpiles,
            canonicalRevisions,
            canonicalCursors);
    }

    private static ImmutableArray<TransportQueuedRequestReadRow> CanonicalizeQueue(
        IEnumerable<TransportQueuedRequestReadRow>? rows)
    {
        return (rows ?? Array.Empty<TransportQueuedRequestReadRow>())
            .OrderBy(static row => row.Request, TransportRequestReadRowComparer.Instance)
            .ThenBy(static row => row.QueueOrder)
            .ToImmutableArray();
    }

    private static ImmutableArray<TransportBacklogRequestReadRow> CanonicalizeBacklog(
        IEnumerable<TransportBacklogRequestReadRow>? rows)
    {
        return (rows ?? Array.Empty<TransportBacklogRequestReadRow>())
            .OrderBy(static row => row.Request, TransportRequestReadRowComparer.Instance)
            .ThenBy(static row => row.EnqueuedTick)
            .ThenBy(static row => row.BacklogOrder)
            .ToImmutableArray();
    }

    private static void EnsureUnique<TRow, TKey>(
        ImmutableArray<TRow> rows,
        Func<TRow, TKey> keySelector,
        string identityName)
        where TKey : notnull
    {
        var seen = new HashSet<TKey>();
        foreach (var row in rows)
        {
            if (!seen.Add(keySelector(row)))
            {
                throw new InvalidOperationException(
                    $"Transport planning snapshot contains duplicate {identityName} identity.");
            }
        }
    }
}

internal sealed class TransportRequestReadRowComparer : IComparer<TransportRequestReadRow>
{
    internal static TransportRequestReadRowComparer Instance { get; } = new();

    int IComparer<TransportRequestReadRow>.Compare(TransportRequestReadRow left, TransportRequestReadRow right)
    {
        int comparison = left.Priority.CompareTo(right.Priority);
        if (comparison != 0) return comparison;
        comparison = left.CreatedTick.CompareTo(right.CreatedTick);
        if (comparison != 0) return comparison;
        comparison = left.SystemOrder.CompareTo(right.SystemOrder);
        if (comparison != 0) return comparison;
        comparison = string.CompareOrdinal(left.ProducerId, right.ProducerId);
        if (comparison != 0) return comparison;
        comparison = left.ItemId.CompareTo(right.ItemId);
        if (comparison != 0) return comparison;
        return left.LocalSequence.CompareTo(right.LocalSequence);
    }
}
