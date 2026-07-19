using System.Collections.Immutable;
using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport.Planning;

internal enum TransportIntentKind : byte
{
    AssignRequest,
    AdvanceMovement,
    ContinuePendingSplit,
    ReplanMovement
}

internal enum TransportActiveDecisionKind : byte
{
    AdvanceMovement,
    ReplanMovement,
    ContinuePendingSplit,
    CleanupInvalid,
    RejectedNoMutation
}

internal enum TransportIntentResourceKind : byte
{
    Item,
    Creature,
    MovementCursor,
    StockpileCell
}

internal enum TransportIntentRejectionReason : byte
{
    InvalidRequest,
    MissingItem,
    ItemUnavailable,
    ItemAlreadyReserved,
    MissingCreature,
    NoEligibleCreature,
    CreatureAlreadyReserved,
    MissingStockpileCell,
    StockpileFilterRejected,
    StockpileCellOccupied,
    MissingMovementCursor,
    NavigationRevisionChanged,
    ResourceConflict,
    DuplicateOrderKey
}

/// <summary>
/// Complete lower-first resolver order. Identity is the full 128-bit GUID; no
/// truncated hash or byte-sized priority participates in authority ordering.
/// </summary>
internal readonly record struct TransportIntentOrderKey(
    int Priority,
    ulong CreatedTick,
    int SystemOrder,
    string ProducerId,
    Guid Identity,
    ulong LocalSequence);

internal readonly record struct TransportIntentResourceClaim(
    TransportIntentResourceKind Kind,
    Guid Identity,
    Point3 Position)
{
    internal static TransportIntentResourceClaim Item(Guid itemId) =>
        new(TransportIntentResourceKind.Item, itemId, Point3.Zero);

    internal static TransportIntentResourceClaim Creature(Guid creatureId) =>
        new(TransportIntentResourceKind.Creature, creatureId, Point3.Zero);

    internal static TransportIntentResourceClaim Movement(Guid creatureId) =>
        new(TransportIntentResourceKind.MovementCursor, creatureId, Point3.Zero);

    internal static TransportIntentResourceClaim Stockpile(Point3 position) =>
        new(TransportIntentResourceKind.StockpileCell, Guid.Empty, position);
}

internal readonly record struct TransportItemExpectation(
    ulong EntityKey,
    Point3 Position,
    int StackCount,
    bool IsOnGround,
    Guid CarrierId);

internal readonly record struct TransportCreatureExpectation(
    ulong EntityKey,
    Point3 Position,
    int HitPoints);

internal enum TransportStockpileExpectationKind : byte
{
    None,
    Absent,
    Present
}

internal readonly record struct TransportStockpileExpectation(
    TransportStockpileExpectationKind Kind,
    int ZoneId,
    Point3 Position,
    uint Generation,
    Guid OccupyingItemId)
{
    internal bool IsRequired => Kind == TransportStockpileExpectationKind.Present;

    internal static TransportStockpileExpectation None { get; } = new(
        TransportStockpileExpectationKind.None,
        ZoneId: 0,
        Position: Point3.Zero,
        Generation: 0,
        OccupyingItemId: Guid.Empty);

    internal static TransportStockpileExpectation Absent(Point3 position) => new(
        TransportStockpileExpectationKind.Absent,
        ZoneId: 0,
        Position: position,
        Generation: 0,
        OccupyingItemId: Guid.Empty);

    internal static TransportStockpileExpectation Present(
        int zoneId,
        Point3 position,
        uint generation,
        Guid occupyingItemId) => new(
            TransportStockpileExpectationKind.Present,
            zoneId,
            position,
            generation,
            occupyingItemId);
}

internal readonly record struct TransportPendingSplitExpectation(
    bool IsRequired,
    Guid ItemId,
    ulong ReservationGeneration,
    ulong IssuedTick)
{
    internal static TransportPendingSplitExpectation None { get; } = new(
        IsRequired: false,
        ItemId: Guid.Empty,
        ReservationGeneration: 0,
        IssuedTick: 0);
}

internal readonly record struct TransportNavigationExpectation(
    ChunkKey StartChunk,
    ulong StartConnectivityVersion,
    ChunkKey GoalChunk,
    ulong GoalConnectivityVersion);

internal readonly record struct TransportMovementExpectation(
    bool IsRequired,
    ulong CursorRevision,
    Point3 Position,
    Point3 Destination,
    uint PathHash,
    int CurrentStep,
    int StepCount)
{
    internal static TransportMovementExpectation None { get; } = new(
        IsRequired: false,
        CursorRevision: 0,
        Position: Point3.Zero,
        Destination: Point3.Zero,
        PathHash: 0,
        CurrentStep: -1,
        StepCount: 0);
}

internal sealed record TransportIntent
{
    private TransportIntent(
        TransportIntentOrderKey orderKey,
        TransportIntentKind kind,
        TransportPendingSource source,
        Guid itemId,
        Guid creatureId,
        Point3 sourcePosition,
        Point3 destination,
        int quantity,
        TransportReason reason,
        byte pathSearchAttempt,
        int planningWorkOrder,
        int candidateOrder,
        TransportItemExpectation expectedItem,
        TransportCreatureExpectation expectedCreature,
        TransportStockpileExpectation expectedStockpile,
        TransportPendingSplitExpectation expectedPendingSplit,
        TransportNavigationExpectation expectedNavigation,
        TransportMovementExpectation expectedMovement,
        ImmutableArray<TransportIntentResourceClaim> claims)
    {
        OrderKey = orderKey;
        Kind = kind;
        Source = source;
        ItemId = itemId;
        CreatureId = creatureId;
        SourcePosition = sourcePosition;
        Destination = destination;
        Quantity = quantity;
        Reason = reason;
        PathSearchAttempt = pathSearchAttempt;
        PlanningWorkOrder = planningWorkOrder;
        CandidateOrder = candidateOrder;
        ExpectedItem = expectedItem;
        ExpectedCreature = expectedCreature;
        ExpectedStockpile = expectedStockpile;
        ExpectedPendingSplit = expectedPendingSplit;
        ExpectedNavigation = expectedNavigation;
        ExpectedMovement = expectedMovement;
        Claims = claims;
    }

    internal TransportIntentOrderKey OrderKey { get; }
    internal TransportIntentKind Kind { get; }
    internal TransportPendingSource Source { get; }
    internal Guid ItemId { get; }
    internal Guid CreatureId { get; }
    internal Point3 SourcePosition { get; }
    internal Point3 Destination { get; }
    internal int Quantity { get; }
    internal TransportReason Reason { get; }
    internal byte PathSearchAttempt { get; }
    internal int PlanningWorkOrder { get; }
    internal int CandidateOrder { get; }
    internal TransportItemExpectation ExpectedItem { get; }
    internal TransportCreatureExpectation ExpectedCreature { get; }
    internal TransportStockpileExpectation ExpectedStockpile { get; }
    internal TransportPendingSplitExpectation ExpectedPendingSplit { get; }
    internal TransportNavigationExpectation ExpectedNavigation { get; }
    internal TransportMovementExpectation ExpectedMovement { get; }
    internal ImmutableArray<TransportIntentResourceClaim> Claims { get; }

    // Compatibility aliases keep the initial Commit integration compiling while
    // callers migrate to validating the complete expectation records.
    internal ulong ExpectedItemEntityKey => ExpectedItem.EntityKey;
    internal ulong ExpectedCreatureEntityKey => ExpectedCreature.EntityKey;
    internal uint ExpectedStockpileGeneration => ExpectedStockpile.Generation;
    internal ulong ExpectedNavigationRevision => ExpectedNavigation.StartConnectivityVersion;
    internal int ExpectedMovementStep => ExpectedMovement.CurrentStep;

    internal static TransportIntent Create(
        TransportIntentOrderKey orderKey,
        TransportIntentKind kind,
        TransportPendingSource source,
        Guid itemId,
        Guid creatureId,
        Point3 sourcePosition,
        Point3 destination,
        int quantity,
        TransportReason reason,
        byte pathSearchAttempt,
        int planningWorkOrder,
        int candidateOrder,
        TransportItemExpectation expectedItem,
        TransportCreatureExpectation expectedCreature,
        TransportStockpileExpectation expectedStockpile,
        TransportPendingSplitExpectation expectedPendingSplit,
        TransportNavigationExpectation expectedNavigation,
        TransportMovementExpectation expectedMovement,
        IEnumerable<TransportIntentResourceClaim> claims)
    {
        if (string.IsNullOrWhiteSpace(orderKey.ProducerId))
            throw new ArgumentException("Transport intent ProducerId is required.", nameof(orderKey));
        if (orderKey.Identity == Guid.Empty)
            throw new ArgumentException("Transport intent full identity is required.", nameof(orderKey));

        var canonicalClaims = (claims ?? throw new ArgumentNullException(nameof(claims)))
            .Distinct()
            .OrderBy(static claim => claim, TransportIntentResourceClaimComparer.Instance)
            .ToImmutableArray();
        if (canonicalClaims.IsEmpty)
            throw new ArgumentException("Transport intent must claim at least one authority resource.", nameof(claims));

        return new TransportIntent(
            orderKey,
            kind,
            source,
            itemId,
            creatureId,
            sourcePosition,
            destination,
            quantity,
            reason,
            pathSearchAttempt,
            planningWorkOrder,
            candidateOrder,
            expectedItem,
            expectedCreature,
            expectedStockpile,
            expectedPendingSplit,
            expectedNavigation,
            expectedMovement,
            canonicalClaims);
    }
}

internal readonly record struct TransportIntentRejection(
    TransportIntentOrderKey OrderKey,
    TransportIntentKind IntentKind,
    Guid ItemId,
    Guid CreatureId,
    TransportIntentRejectionReason Reason,
    TransportIntentOrderKey? ConflictingWinner,
    string DetailCode);

internal sealed record TransportAssignmentPlan(
    TransportIntent Winner,
    ImmutableArray<TransportIntent> OrderedCandidates);

internal readonly record struct TransportActivePlanDecision(
    Guid ItemId,
    Guid CreatureId,
    TransportActiveDecisionKind Kind,
    TransportIntent? AcceptedIntent,
    TransportIntentRejection? Rejection)
{
    internal bool IsAccepted => AcceptedIntent != null;
}

internal sealed record TransportPlanResolution(
    ImmutableArray<TransportIntent> Accepted,
    ImmutableArray<TransportIntentRejection> Rejected,
    ImmutableArray<TransportAssignmentPlan> AssignmentPlans,
    ImmutableArray<TransportActivePlanDecision> ActiveDecisions)
{
    internal bool TryGetActiveDecision(
        Guid itemId,
        Guid creatureId,
        out TransportActivePlanDecision decision)
    {
        foreach (var candidate in ActiveDecisions)
        {
            if (candidate.ItemId == itemId && candidate.CreatureId == creatureId)
            {
                decision = candidate;
                return true;
            }
        }

        decision = default;
        return false;
    }
}

internal sealed class TransportIntentOrderKeyComparer : IComparer<TransportIntentOrderKey>
{
    internal static TransportIntentOrderKeyComparer Instance { get; } = new();

    int IComparer<TransportIntentOrderKey>.Compare(TransportIntentOrderKey left, TransportIntentOrderKey right)
    {
        int comparison = left.Priority.CompareTo(right.Priority);
        if (comparison != 0) return comparison;
        comparison = left.CreatedTick.CompareTo(right.CreatedTick);
        if (comparison != 0) return comparison;
        comparison = left.SystemOrder.CompareTo(right.SystemOrder);
        if (comparison != 0) return comparison;
        comparison = string.CompareOrdinal(left.ProducerId, right.ProducerId);
        if (comparison != 0) return comparison;
        comparison = left.Identity.CompareTo(right.Identity);
        if (comparison != 0) return comparison;
        return left.LocalSequence.CompareTo(right.LocalSequence);
    }
}

internal sealed class TransportIntentResourceClaimComparer : IComparer<TransportIntentResourceClaim>
{
    internal static TransportIntentResourceClaimComparer Instance { get; } = new();

    internal int Compare(TransportIntentResourceClaim left, TransportIntentResourceClaim right)
    {
        int comparison = left.Kind.CompareTo(right.Kind);
        if (comparison != 0) return comparison;
        comparison = left.Identity.CompareTo(right.Identity);
        if (comparison != 0) return comparison;
        comparison = left.Position.Z.CompareTo(right.Position.Z);
        if (comparison != 0) return comparison;
        comparison = left.Position.Y.CompareTo(right.Position.Y);
        if (comparison != 0) return comparison;
        return left.Position.X.CompareTo(right.Position.X);
    }

    int IComparer<TransportIntentResourceClaim>.Compare(
        TransportIntentResourceClaim left,
        TransportIntentResourceClaim right) => Compare(left, right);
}
