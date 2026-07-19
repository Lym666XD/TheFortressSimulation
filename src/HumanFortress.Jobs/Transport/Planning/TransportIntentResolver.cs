using System.Collections.Immutable;

namespace HumanFortress.Jobs.Transport.Planning;

/// <summary>
/// Stable, single-authority conflict resolver. Planner completion order never
/// participates in winner selection.
/// </summary>
internal static class TransportIntentResolver
{
    internal static TransportPlanResolution Resolve(
        IEnumerable<TransportIntent> intents,
        IEnumerable<TransportIntentRejection>? planningRejections = null)
    {
        ArgumentNullException.ThrowIfNull(intents);

        var candidates = intents.ToArray();

        var accepted = ImmutableArray.CreateBuilder<TransportIntent>();
        var rejected = ImmutableArray.CreateBuilder<TransportIntentRejection>();
        if (planningRejections != null)
            rejected.AddRange(planningRejections);

        var winnerByResource = new Dictionary<TransportIntentResourceClaim, TransportIntentOrderKey>();
        var groups = candidates
            .GroupBy(static intent => intent.OrderKey)
            .OrderBy(static group => group.Key, TransportIntentOrderKeyComparer.Instance);

        foreach (var group in groups)
        {
            var sameKey = group
                .OrderBy(static intent => intent, TransportIntentPayloadComparer.Instance)
                .ToArray();
            if (sameKey.Length != 1)
            {
                foreach (var duplicate in sameKey)
                {
                    rejected.Add(ToRejection(
                        duplicate,
                        TransportIntentRejectionReason.DuplicateOrderKey,
                        conflictingWinner: null,
                        "duplicate-order-key"));
                }

                continue;
            }

            var candidate = sameKey[0];
            TransportIntentOrderKey? conflictingWinner = null;
            foreach (var claim in candidate.Claims)
            {
                if (winnerByResource.TryGetValue(claim, out var winner))
                {
                    conflictingWinner = winner;
                    break;
                }
            }

            if (conflictingWinner.HasValue)
            {
                rejected.Add(ToRejection(
                    candidate,
                    TransportIntentRejectionReason.ResourceConflict,
                    conflictingWinner,
                    "resource-conflict"));
                continue;
            }

            accepted.Add(candidate);
            foreach (var claim in candidate.Claims)
                winnerByResource.Add(claim, candidate.OrderKey);
        }

        var acceptedIntents = accepted.ToImmutable();
        var rejectedIntents = rejected
            .OrderBy(static rejection => rejection.OrderKey, TransportIntentOrderKeyComparer.Instance)
            .ThenBy(static rejection => rejection.Reason)
            .ThenBy(static rejection => rejection.ItemId)
            .ThenBy(static rejection => rejection.CreatureId)
            .ThenBy(static rejection => rejection.DetailCode, StringComparer.Ordinal)
            .ToImmutableArray();
        return new TransportPlanResolution(
            acceptedIntents,
            rejectedIntents,
            BuildAssignmentPlans(candidates, acceptedIntents, winnerByResource),
            BuildActiveDecisions(acceptedIntents, rejectedIntents));
    }

    private static ImmutableArray<TransportAssignmentPlan> BuildAssignmentPlans(
        IReadOnlyList<TransportIntent> candidates,
        ImmutableArray<TransportIntent> accepted,
        IReadOnlyDictionary<TransportIntentResourceClaim, TransportIntentOrderKey> winnerByResource)
    {
        var plans = ImmutableArray.CreateBuilder<TransportAssignmentPlan>();
        var fallbackOwnerByResource = new Dictionary<TransportIntentResourceClaim, TransportIntentOrderKey>();
        foreach (var winner in accepted.Where(static intent => intent.Kind == TransportIntentKind.AssignRequest))
        {
            var ordered = ImmutableArray.CreateBuilder<TransportIntent>();
            ordered.Add(winner);
            foreach (var candidate in candidates
                         .Where(intent => intent.Kind == TransportIntentKind.AssignRequest
                             && intent.Source == winner.Source
                             && intent.ItemId == winner.ItemId
                             && intent.PlanningWorkOrder == winner.PlanningWorkOrder
                             && !ReferenceEquals(intent, winner))
                         .OrderBy(static intent => intent.CandidateOrder)
                         .ThenBy(static intent => intent.OrderKey, TransportIntentOrderKeyComparer.Instance))
            {
                if (!ClaimsRemainAvailable(candidate, winner, winnerByResource, fallbackOwnerByResource))
                    continue;

                ordered.Add(candidate);
                foreach (var claim in candidate.Claims)
                {
                    if (!winnerByResource.ContainsKey(claim))
                        fallbackOwnerByResource.TryAdd(claim, winner.OrderKey);
                }
            }

            plans.Add(new TransportAssignmentPlan(winner, ordered.ToImmutable()));
        }

        return plans.ToImmutable();
    }

    private static bool ClaimsRemainAvailable(
        TransportIntent candidate,
        TransportIntent winner,
        IReadOnlyDictionary<TransportIntentResourceClaim, TransportIntentOrderKey> winnerByResource,
        IReadOnlyDictionary<TransportIntentResourceClaim, TransportIntentOrderKey> fallbackOwnerByResource)
    {
        foreach (var claim in candidate.Claims)
        {
            if (winnerByResource.TryGetValue(claim, out var acceptedOwner)
                && acceptedOwner != winner.OrderKey)
            {
                return false;
            }

            if (fallbackOwnerByResource.TryGetValue(claim, out var fallbackOwner)
                && fallbackOwner != winner.OrderKey)
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<TransportActivePlanDecision> BuildActiveDecisions(
        ImmutableArray<TransportIntent> accepted,
        ImmutableArray<TransportIntentRejection> rejected)
    {
        var decisions = ImmutableArray.CreateBuilder<TransportActivePlanDecision>();
        foreach (var intent in accepted.Where(static intent => intent.Kind != TransportIntentKind.AssignRequest))
        {
            decisions.Add(new TransportActivePlanDecision(
                intent.ItemId,
                intent.CreatureId,
                intent.Kind switch
                {
                    TransportIntentKind.ContinuePendingSplit =>
                        TransportActiveDecisionKind.ContinuePendingSplit,
                    TransportIntentKind.ReplanMovement =>
                        TransportActiveDecisionKind.ReplanMovement,
                    _ => TransportActiveDecisionKind.AdvanceMovement
                },
                intent,
                Rejection: null));
        }

        foreach (var rejection in rejected.Where(static rejection =>
                     rejection.IntentKind != TransportIntentKind.AssignRequest))
        {
            decisions.Add(new TransportActivePlanDecision(
                rejection.ItemId,
                rejection.CreatureId,
                rejection.Reason is TransportIntentRejectionReason.MissingItem
                    or TransportIntentRejectionReason.MissingCreature
                    or TransportIntentRejectionReason.MissingStockpileCell
                    ? TransportActiveDecisionKind.CleanupInvalid
                    : TransportActiveDecisionKind.RejectedNoMutation,
                AcceptedIntent: null,
                rejection));
        }

        return decisions
            .OrderBy(static decision => decision.AcceptedIntent?.OrderKey
                ?? decision.Rejection!.Value.OrderKey, TransportIntentOrderKeyComparer.Instance)
            .ThenBy(static decision => decision.ItemId)
            .ThenBy(static decision => decision.CreatureId)
            .ToImmutableArray();
    }

    private static TransportIntentRejection ToRejection(
        TransportIntent intent,
        TransportIntentRejectionReason reason,
        TransportIntentOrderKey? conflictingWinner,
        string detailCode)
    {
        return new TransportIntentRejection(
            intent.OrderKey,
            intent.Kind,
            intent.ItemId,
            intent.CreatureId,
            reason,
            conflictingWinner,
            detailCode);
    }
}

internal sealed class TransportIntentPayloadComparer : IComparer<TransportIntent>
{
    internal static TransportIntentPayloadComparer Instance { get; } = new();

    int IComparer<TransportIntent>.Compare(TransportIntent? left, TransportIntent? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;

        int comparison = left.Kind.CompareTo(right.Kind);
        if (comparison != 0) return comparison;
        comparison = left.ItemId.CompareTo(right.ItemId);
        if (comparison != 0) return comparison;
        comparison = left.CreatureId.CompareTo(right.CreatureId);
        if (comparison != 0) return comparison;
        comparison = ComparePoint(left.SourcePosition, right.SourcePosition);
        if (comparison != 0) return comparison;
        comparison = ComparePoint(left.Destination, right.Destination);
        if (comparison != 0) return comparison;
        comparison = left.Quantity.CompareTo(right.Quantity);
        if (comparison != 0) return comparison;
        comparison = left.Reason.CompareTo(right.Reason);
        if (comparison != 0) return comparison;
        comparison = left.PathSearchAttempt.CompareTo(right.PathSearchAttempt);
        if (comparison != 0) return comparison;
        comparison = left.PlanningWorkOrder.CompareTo(right.PlanningWorkOrder);
        if (comparison != 0) return comparison;
        comparison = left.CandidateOrder.CompareTo(right.CandidateOrder);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedItem.EntityKey.CompareTo(right.ExpectedItem.EntityKey);
        if (comparison != 0) return comparison;
        comparison = ComparePoint(left.ExpectedItem.Position, right.ExpectedItem.Position);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedItem.StackCount.CompareTo(right.ExpectedItem.StackCount);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedItem.IsOnGround.CompareTo(right.ExpectedItem.IsOnGround);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedItem.CarrierId.CompareTo(right.ExpectedItem.CarrierId);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedCreature.EntityKey.CompareTo(right.ExpectedCreature.EntityKey);
        if (comparison != 0) return comparison;
        comparison = ComparePoint(left.ExpectedCreature.Position, right.ExpectedCreature.Position);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedCreature.HitPoints.CompareTo(right.ExpectedCreature.HitPoints);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedStockpile.Kind.CompareTo(right.ExpectedStockpile.Kind);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedStockpile.ZoneId.CompareTo(right.ExpectedStockpile.ZoneId);
        if (comparison != 0) return comparison;
        comparison = ComparePoint(left.ExpectedStockpile.Position, right.ExpectedStockpile.Position);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedStockpile.Generation.CompareTo(right.ExpectedStockpile.Generation);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedStockpile.OccupyingItemId.CompareTo(right.ExpectedStockpile.OccupyingItemId);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedPendingSplit.IsRequired.CompareTo(right.ExpectedPendingSplit.IsRequired);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedPendingSplit.ItemId.CompareTo(right.ExpectedPendingSplit.ItemId);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedPendingSplit.ReservationGeneration.CompareTo(
            right.ExpectedPendingSplit.ReservationGeneration);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedPendingSplit.IssuedTick.CompareTo(right.ExpectedPendingSplit.IssuedTick);
        if (comparison != 0) return comparison;
        comparison = CompareChunk(left.ExpectedNavigation.StartChunk, right.ExpectedNavigation.StartChunk);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedNavigation.StartConnectivityVersion.CompareTo(
            right.ExpectedNavigation.StartConnectivityVersion);
        if (comparison != 0) return comparison;
        comparison = CompareChunk(left.ExpectedNavigation.GoalChunk, right.ExpectedNavigation.GoalChunk);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedNavigation.GoalConnectivityVersion.CompareTo(
            right.ExpectedNavigation.GoalConnectivityVersion);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedMovement.IsRequired.CompareTo(right.ExpectedMovement.IsRequired);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedMovement.CursorRevision.CompareTo(right.ExpectedMovement.CursorRevision);
        if (comparison != 0) return comparison;
        comparison = ComparePoint(left.ExpectedMovement.Position, right.ExpectedMovement.Position);
        if (comparison != 0) return comparison;
        comparison = ComparePoint(left.ExpectedMovement.Destination, right.ExpectedMovement.Destination);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedMovement.PathHash.CompareTo(right.ExpectedMovement.PathHash);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedMovement.CurrentStep.CompareTo(right.ExpectedMovement.CurrentStep);
        if (comparison != 0) return comparison;
        comparison = left.ExpectedMovement.StepCount.CompareTo(right.ExpectedMovement.StepCount);
        if (comparison != 0) return comparison;
        comparison = left.Claims.Length.CompareTo(right.Claims.Length);
        if (comparison != 0) return comparison;
        for (int index = 0; index < left.Claims.Length; index++)
        {
            comparison = TransportIntentResourceClaimComparer.Instance.Compare(
                left.Claims[index],
                right.Claims[index]);
            if (comparison != 0) return comparison;
        }

        return 0;
    }

    private static int ComparePoint(
        HumanFortress.Contracts.Navigation.Point3 left,
        HumanFortress.Contracts.Navigation.Point3 right)
    {
        int comparison = left.Z.CompareTo(right.Z);
        if (comparison != 0) return comparison;
        comparison = left.Y.CompareTo(right.Y);
        if (comparison != 0) return comparison;
        return left.X.CompareTo(right.X);
    }

    private static int CompareChunk(
        HumanFortress.Contracts.Navigation.ChunkKey left,
        HumanFortress.Contracts.Navigation.ChunkKey right)
    {
        int comparison = left.Z.CompareTo(right.Z);
        if (comparison != 0) return comparison;
        comparison = left.ChunkY.CompareTo(right.ChunkY);
        if (comparison != 0) return comparison;
        return left.ChunkX.CompareTo(right.ChunkX);
    }
}
