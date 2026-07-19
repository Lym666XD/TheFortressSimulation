using System.Collections.Immutable;
using System.Reflection;
using HumanFortress.Contracts.Navigation;
using HumanFortress.Jobs.Transport.Planning;
using HumanFortress.Simulation.Jobs;

internal static class TransportIntentPipelineRegressionTests
{
    public static void RunAll()
    {
        Console.WriteLine("=== Transport Intent Pipeline Regression Tests ===");

        TestSnapshotCanonicalizesEveryAuthoritySurface();
        TestResolverKeepsFullIntPriorityLowerFirst();
        TestResolverUsesCompleteStableTuple();
        TestResolverWinnerIgnoresInputPermutation();
        TestWorkerCountsAndForcedCompletionOrderAreEquivalent();
        TestAssignmentPlansExposeConflictSafeFallbackCandidates();
        TestAssignmentIntentCarriesCompleteCommitExpectations();
        TestStockpileIndexedReasonsCarryCompleteAuthorityExpectations();
        TestActiveDecisionsAreExhaustiveAndStockpileAware();
        TestPurePlannerDoesNotOwnMutableRuntimeServices();

        Console.WriteLine("=== Transport Intent Pipeline Regression Tests Completed ===\n");
    }

    private static void TestSnapshotCanonicalizesEveryAuthoritySurface()
    {
        var itemA = Id(1);
        var itemB = Id(2);
        var creatureA = Id(101);
        var creatureB = Id(102);
        var requestA = Request(itemA, new Point3(1, 1, 0), new Point3(8, 1, 0), 1000, 2);
        var requestB = Request(itemB, new Point3(2, 1, 0), new Point3(9, 1, 0), 1400, 1);
        var queue = new[]
        {
            new TransportQueuedRequestReadRow(9, requestB),
            new TransportQueuedRequestReadRow(4, requestA)
        };
        var acceptedDefinitions = new[] { "item.b", "item.a", "item.a" }.ToImmutableArray();

        var snapshot = TransportPlanningSnapshot.Create(
            tick: 42,
            queuedRequests: queue,
            backlogRequests: new[] { new TransportBacklogRequestReadRow(7, 12, requestB) },
            activeJobs: new[]
            {
                new TransportActiveJobReadRow(
                    itemB,
                    creatureB,
                    new Point3(9, 1, 0),
                    HumanFortress.Jobs.Transport.JobStage.ToDest,
                    1,
                    TransportReason.Misc,
                    1400,
                    1,
                    300,
                    "producer.b",
                    4,
                    0,
                    0)
            },
            items: new[]
            {
                new TransportItemReadRow(itemB, 22, "item.b", new Point3(2, 1, 0), 1, true, Guid.Empty),
                new TransportItemReadRow(itemA, 11, "item.a", new Point3(1, 1, 0), 1, true, Guid.Empty)
            },
            creatures: new[]
            {
                new TransportCreatureReadRow(creatureB, 202, new Point3(4, 1, 0), 10, true),
                new TransportCreatureReadRow(creatureA, 201, new Point3(3, 1, 0), 10, true)
            },
            reservations: new[]
            {
                new TransportReservationReadRow(
                    TransportReservationResourceKind.Creature,
                    creatureB,
                    Guid.Empty,
                    "Jobs.Transport",
                    "haul:b",
                    8,
                    100,
                    false)
            },
            stockpileCells: new[]
            {
                new TransportStockpileCellReadRow(
                    4,
                    new Point3(9, 1, 0),
                    3,
                    acceptedDefinitions,
                    Guid.Empty)
            },
            navigationRevisions: new[]
            {
                new TransportNavigationRevisionReadRow(new ChunkKey(0, 0, 0), 19)
            },
            movementCursors: new[]
            {
                new TransportMovementCursorReadRow(
                    creatureB,
                    202,
                    5,
                    new Point3(4, 1, 0),
                    new Point3(9, 1, 0),
                    7,
                    2,
                    8,
                    1,
                    0,
                    2,
                    new ChunkKey(0, 0, 0),
                    19)
            });

        queue[0] = new TransportQueuedRequestReadRow(0, requestA);
        RegressionAssert.True(snapshot.Tick == 42, "Transport snapshot lost its tick identity.");
        RegressionAssert.True(
            snapshot.QueuedRequests.Select(static row => row.Request.ItemId).SequenceEqual(new[] { itemA, itemB }),
            "Transport snapshot did not canonicalize queued requests independently of source order.");
        RegressionAssert.True(
            snapshot.Items.Select(static row => row.ItemId).SequenceEqual(new[] { itemA, itemB })
            && snapshot.Creatures.Select(static row => row.CreatureId).SequenceEqual(new[] { creatureA, creatureB }),
            "Transport snapshot did not canonicalize item and creature authority rows.");
        RegressionAssert.True(
            snapshot.BacklogRequests.Length == 1
            && snapshot.ActiveJobs.Length == 1
            && snapshot.Reservations.Length == 1
            && snapshot.StockpileCells.Length == 1
            && snapshot.NavigationRevisions.Length == 1
            && snapshot.MovementCursors.Length == 1,
            "Transport snapshot omitted an authority surface required by planning.");
        RegressionAssert.True(
            snapshot.StockpileCells[0].AcceptedDefinitionIds.SequenceEqual(new[] { "item.a", "item.b" }),
            "Transport snapshot did not freeze and canonicalize stockpile filter identity.");

        Console.WriteLine("[PASS] Transport planning snapshot is complete and canonical");
    }

    private static void TestResolverKeepsFullIntPriorityLowerFirst()
    {
        var sharedItem = Id(10);
        var priority1600 = Intent(1600, 0, 0, "producer", Id(1600), 0, sharedItem, Id(201));
        var priority1000 = Intent(1000, 0, 0, "producer", Id(1000), 0, sharedItem, Id(202));
        var priority1400 = Intent(1400, 0, 0, "producer", Id(1400), 0, sharedItem, Id(203));

        var result = TransportIntentResolver.Resolve(new[] { priority1600, priority1000, priority1400 });

        RegressionAssert.True(
            result.Accepted.Length == 1 && result.Accepted[0].OrderKey.Priority == 1000,
            "Transport resolver narrowed or reversed full int priorities 1000/1400/1600.");
        RegressionAssert.True(
            result.Rejected.Length == 2
            && result.Rejected.All(static rejection =>
                rejection.Reason == TransportIntentRejectionReason.ResourceConflict),
            "Transport resolver did not explicitly reject losing priority candidates.");

        Console.WriteLine("[PASS] Transport resolver keeps full int priority lower-first");
    }

    private static void TestResolverUsesCompleteStableTuple()
    {
        var intents = new[]
        {
            Intent(1, 1, 1, "a", Id(1), 1, Id(301), Id(401)),
            Intent(2, 0, 0, "a", Id(2), 0, Id(302), Id(402)),
            Intent(1, 2, 0, "a", Id(3), 0, Id(303), Id(403)),
            Intent(1, 1, 2, "a", Id(4), 0, Id(304), Id(404)),
            Intent(1, 1, 1, "b", Id(5), 0, Id(305), Id(405)),
            Intent(1, 1, 1, "a", Id(6), 0, Id(306), Id(406)),
            Intent(1, 1, 1, "a", Id(1), 0, Id(307), Id(407))
        };

        var result = TransportIntentResolver.Resolve(intents);
        var ordered = result.Accepted.Select(static intent => intent.OrderKey).ToArray();

        RegressionAssert.True(result.Rejected.IsEmpty, "Non-conflicting tuple-order probes were rejected.");
        RegressionAssert.True(
            ordered.SequenceEqual(new[]
            {
                intents[6].OrderKey,
                intents[0].OrderKey,
                intents[5].OrderKey,
                intents[4].OrderKey,
                intents[3].OrderKey,
                intents[2].OrderKey,
                intents[1].OrderKey
            }),
            "Transport resolver did not use Priority, CreatedTick, SystemOrder, ordinal ProducerId, full identity, and LocalSequence in order.");

        Console.WriteLine("[PASS] Transport resolver uses the complete stable tuple");
    }

    private static void TestResolverWinnerIgnoresInputPermutation()
    {
        var sharedItem = Id(500);
        var candidates = new[]
        {
            Intent(1400, 4, 20, "producer.c", Id(503), 2, sharedItem, Id(603)),
            Intent(1000, 4, 20, "producer.a", Id(501), 0, sharedItem, Id(601)),
            Intent(1000, 5, 10, "producer.b", Id(502), 1, sharedItem, Id(602))
        };
        string? expected = null;
        foreach (var permutation in Permutations(candidates))
        {
            string signature = ResolutionSignature(TransportIntentResolver.Resolve(permutation));
            expected ??= signature;
            RegressionAssert.True(
                string.Equals(signature, expected, StringComparison.Ordinal),
                "Transport resolver changed winners or rejections with input permutation.");
        }

        Console.WriteLine("[PASS] Transport resolver ignores input permutation");
    }

    private static void TestWorkerCountsAndForcedCompletionOrderAreEquivalent()
    {
        var canonical = CreateParallelSnapshot(reverseInputs: false);
        var permuted = CreateParallelSnapshot(reverseInputs: true);
        var serial = TransportPlanningPipeline.Plan(canonical);
        string expected = ResolutionSignature(serial);

        foreach (int workerCount in new[] { 1, 2, 4 })
        {
            var result = TransportPlanningPipeline.PlanAsync(
                    workerCount == 4 ? permuted : canonical,
                    workerCount,
                    DelayWorkersInReverseOrder)
                .GetAwaiter()
                .GetResult();
            RegressionAssert.True(
                string.Equals(ResolutionSignature(result), expected, StringComparison.Ordinal),
                $"Transport planning changed under {workerCount} workers or forced completion order.");
        }

        RegressionAssert.True(
            serial.Accepted.Length == 4
            && serial.Accepted.Select(static intent => intent.ItemId).Distinct().Count() == 4
            && serial.Accepted.Select(static intent => intent.CreatureId).Distinct().Count() == 4,
            "Transport planner/resolver did not produce a conflict-free deterministic assignment set.");

        Console.WriteLine("[PASS] Transport planner is equivalent with 1/2/4 workers and forced delays");
    }

    private static void TestAssignmentPlansExposeConflictSafeFallbackCandidates()
    {
        var itemA = Id(701);
        var itemB = Id(702);
        var workerA = Id(711);
        var workerB = Id(712);
        var workerC = Id(713);
        var destinationA = new Point3(12, 1, 0);
        var destinationB = new Point3(13, 1, 0);
        var snapshot = TransportPlanningSnapshot.Create(
            tick: 20,
            queuedRequests: new[]
            {
                new TransportQueuedRequestReadRow(
                    0,
                    Request(itemA, new Point3(1, 1, 0), destinationA, 1000, 0)),
                new TransportQueuedRequestReadRow(
                    1,
                    Request(itemB, new Point3(2, 1, 0), destinationB, 1400, 1))
            },
            items: new[]
            {
                new TransportItemReadRow(itemA, 701, "item.ore", new Point3(1, 1, 0), 3, true, Guid.Empty),
                new TransportItemReadRow(itemB, 702, "item.ore", new Point3(2, 1, 0), 2, true, Guid.Empty)
            },
            creatures: new[]
            {
                new TransportCreatureReadRow(workerA, 711, new Point3(1, 4, 0), 10, true),
                new TransportCreatureReadRow(workerB, 712, new Point3(2, 4, 0), 10, true),
                new TransportCreatureReadRow(workerC, 713, new Point3(3, 4, 0), 10, true),
                new TransportCreatureReadRow(Id(714), 714, new Point3(4, 4, 0), 10, false)
            },
            stockpileCells: new[]
            {
                new TransportStockpileCellReadRow(1, destinationA, 4, ImmutableArray.Create("item.ore"), Guid.Empty),
                new TransportStockpileCellReadRow(2, destinationB, 5, ImmutableArray.Create("item.ore"), Guid.Empty)
            },
            navigationRevisions: new[]
            {
                new TransportNavigationRevisionReadRow(new ChunkKey(0, 0, 0), 9)
            });

        var result = TransportPlanningPipeline.Plan(snapshot);

        RegressionAssert.True(
            result.AssignmentPlans.Length == 2,
            "Resolver did not expose one assignment plan per accepted request.");
        RegressionAssert.True(
            result.AssignmentPlans[0].OrderedCandidates.Select(static candidate => candidate.CreatureId)
                .SequenceEqual(new[] { workerA, workerC })
            && result.AssignmentPlans[1].OrderedCandidates.Select(static candidate => candidate.CreatureId)
                .SequenceEqual(new[] { workerB }),
            "Resolver did not retain a deterministic no-path fallback or leaked a winner/fallback worker across plans.");
        RegressionAssert.True(
            result.AssignmentPlans
                .SelectMany(static plan => plan.OrderedCandidates)
                .Select(static candidate => candidate.CreatureId)
                .Distinct()
                .Count() == 3,
            "Resolver fallback resources are not conflict-safe across accepted assignment plans.");

        Console.WriteLine("[PASS] Assignment plans expose conflict-safe path fallback candidates");
    }

    private static void TestAssignmentIntentCarriesCompleteCommitExpectations()
    {
        var result = TransportPlanningPipeline.Plan(CreateParallelSnapshot(reverseInputs: false));
        var intent = result.AssignmentPlans[0].Winner;

        RegressionAssert.True(
            intent.ExpectedItem.EntityKey != 0
            && intent.ExpectedItem.Position == intent.SourcePosition
            && intent.ExpectedItem.StackCount == 1
            && intent.ExpectedItem.IsOnGround
            && intent.ExpectedItem.CarrierId == Guid.Empty,
            "Assignment intent omitted item source/carrier/quantity CAS data.");
        RegressionAssert.True(
            intent.ExpectedCreature.EntityKey != 0
            && intent.ExpectedCreature.Position != Point3.Zero
            && intent.ExpectedCreature.HitPoints == 10,
            "Assignment intent omitted worker position/HP CAS data.");
        RegressionAssert.True(
            intent.ExpectedStockpile.IsRequired
            && intent.ExpectedStockpile.Generation == 1
            && intent.ExpectedNavigation.StartConnectivityVersion == 7
            && intent.ExpectedNavigation.GoalConnectivityVersion == 7
            && !intent.ExpectedMovement.IsRequired,
            "Assignment intent omitted stockpile, navigation, or absent-cursor CAS data.");

        Console.WriteLine("[PASS] Assignment intent carries complete commit expectations");
    }

    private static void TestStockpileIndexedReasonsCarryCompleteAuthorityExpectations()
    {
        var required = new[]
        {
            TransportReason.ToStockpile,
            TransportReason.ToWorkshopOutput,
            TransportReason.FromTradeDepot
        };
        var optional = new[]
        {
            TransportReason.ToArmory,
            TransportReason.ToAmmoCache
        };
        var indexed = required.Concat(optional).ToHashSet();
        RegressionAssert.True(
            Enum.GetValues<TransportReason>().All(reason =>
                TransportDestinationValidator.WritesStockpileIndex(reason) == indexed.Contains(reason)
                && TransportDestinationValidator.RequiresStockpileCell(reason) == required.Contains(reason)),
            "Transport stockpile-index policy drifted between destination and index semantics.");

        foreach (var reason in indexed)
        {
            var itemId = Id(900 + (int)reason);
            var workerId = Id(950 + (int)reason);
            var source = new Point3(1, 1, 0);
            var destination = new Point3(8, 1, 0);
            var request = new TransportQueuedRequestReadRow(
                0,
                Request(itemId, source, destination, 1000, 0, reason));
            var commonItems = new[]
            {
                new TransportItemReadRow(itemId, (ulong)(900 + (int)reason), "item.ore", source, 1, true, Guid.Empty)
            };
            var commonCreatures = new[]
            {
                new TransportCreatureReadRow(workerId, (ulong)(950 + (int)reason), source, 10, true)
            };
            var revisions = new[]
            {
                new TransportNavigationRevisionReadRow(new ChunkKey(0, 0, 0), 7)
            };

            var present = TransportPlanningPipeline.Plan(TransportPlanningSnapshot.Create(
                tick: 10,
                queuedRequests: new[] { request },
                items: commonItems,
                creatures: commonCreatures,
                stockpileCells: new[]
                {
                    new TransportStockpileCellReadRow(
                        4,
                        destination,
                        12,
                        ImmutableArray.Create("item.ore"),
                        Guid.Empty)
                },
                navigationRevisions: revisions));
            var presentIntent = present.AssignmentPlans.Single().Winner;
            RegressionAssert.True(
                presentIntent.ExpectedStockpile.Kind == TransportStockpileExpectationKind.Present
                && presentIntent.ExpectedStockpile.Generation == 12
                && presentIntent.Claims.Contains(TransportIntentResourceClaim.Stockpile(destination)),
                $"Transport reason {reason} omitted present stockpile CAS or conflict claim.");

            var absent = TransportPlanningPipeline.Plan(TransportPlanningSnapshot.Create(
                tick: 10,
                queuedRequests: new[] { request },
                items: commonItems,
                creatures: commonCreatures,
                navigationRevisions: revisions,
                observedNonStockpileDestinations: new[] { destination }));
            if (required.Contains(reason))
            {
                RegressionAssert.True(
                    absent.Accepted.IsEmpty
                    && absent.Rejected.Any(static rejection =>
                        rejection.Reason == TransportIntentRejectionReason.MissingStockpileCell),
                    $"Required stockpile reason {reason} accepted an observed non-stockpile destination.");
            }
            else
            {
                var absentIntent = absent.AssignmentPlans.Single().Winner;
                RegressionAssert.True(
                    absentIntent.ExpectedStockpile.Kind == TransportStockpileExpectationKind.Absent
                    && absentIntent.ExpectedStockpile.Position == destination
                    && !absentIntent.Claims.Contains(TransportIntentResourceClaim.Stockpile(destination)),
                    $"Optional stockpile reason {reason} omitted absent-cell CAS semantics.");
            }
        }

        Console.WriteLine("[PASS] Stockpile-indexed reasons carry complete authority expectations");
    }

    private static void TestActiveDecisionsAreExhaustiveAndStockpileAware()
    {
        var itemId = Id(801);
        var workerId = Id(802);
        var pendingItemId = Id(803);
        var destination = new Point3(40, 1, 0);
        var pendingSnapshot = TransportPlanningSnapshot.Create(
            tick: 31,
            activeJobs: new[]
            {
                new TransportActiveJobReadRow(
                    itemId,
                    workerId,
                    destination,
                    HumanFortress.Jobs.Transport.JobStage.ToItem,
                    2,
                    TransportReason.ToStockpile,
                    0,
                    30,
                    300,
                    "Jobs.Transport",
                    0,
                    0,
                    0,
                    pendingItemId,
                    17,
                    30)
            },
            items: new[]
            {
                new TransportItemReadRow(itemId, 801, "item.ore", new Point3(5, 1, 0), 5, true, Guid.Empty)
            },
            creatures: new[]
            {
                new TransportCreatureReadRow(workerId, 802, new Point3(5, 1, 0), 10, true)
            },
            stockpileCells: new[]
            {
                new TransportStockpileCellReadRow(9, destination, 23, ImmutableArray.Create("item.ore"), Guid.Empty)
            },
            navigationRevisions: new[]
            {
                new TransportNavigationRevisionReadRow(new ChunkKey(0, 0, 0), 11),
                new TransportNavigationRevisionReadRow(new ChunkKey(1, 0, 0), 12)
            });

        var pending = TransportPlanningPipeline.Plan(pendingSnapshot);
        RegressionAssert.True(
            pending.ActiveDecisions.Length == 1
            && pending.ActiveDecisions[0].Kind == TransportActiveDecisionKind.ContinuePendingSplit
            && pending.ActiveDecisions[0].IsAccepted,
            "Pending split without a cursor did not receive an explicit accepted active decision.");
        var pendingIntent = pending.ActiveDecisions[0].AcceptedIntent!;
        RegressionAssert.True(
            pendingIntent.ExpectedPendingSplit.IsRequired
            && pendingIntent.ExpectedPendingSplit.ItemId == pendingItemId
            && pendingIntent.ExpectedPendingSplit.ReservationGeneration == 17
            && pendingIntent.ExpectedStockpile.IsRequired
            && pendingIntent.ExpectedStockpile.Generation == 23
            && pendingIntent.Claims.Contains(TransportIntentResourceClaim.Item(pendingItemId))
            && pendingIntent.Claims.Contains(TransportIntentResourceClaim.Stockpile(destination)),
            "Pending split intent omitted staged reservation or active destination authority.");

        var missingCursorSnapshot = TransportPlanningSnapshot.Create(
            tick: 32,
            activeJobs: pendingSnapshot.ActiveJobs.Select(static job => job with
            {
                Reason = TransportReason.Misc,
                PendingSplitItemId = Guid.Empty,
                PendingSplitGeneration = 0,
                PendingSplitIssuedTick = 0
            }),
            items: pendingSnapshot.Items,
            creatures: pendingSnapshot.Creatures,
            navigationRevisions: pendingSnapshot.NavigationRevisions);
        var missingCursor = TransportPlanningPipeline.Plan(missingCursorSnapshot);
        RegressionAssert.True(
            missingCursor.ActiveDecisions.Length == 1
            && missingCursor.ActiveDecisions[0].Kind == TransportActiveDecisionKind.ReplanMovement
            && missingCursor.ActiveDecisions[0].IsAccepted,
            "Missing active movement cursor was not expressed as an accepted replan decision.");

        var missingItemSnapshot = TransportPlanningSnapshot.Create(
            tick: 33,
            activeJobs: missingCursorSnapshot.ActiveJobs,
            creatures: pendingSnapshot.Creatures,
            navigationRevisions: pendingSnapshot.NavigationRevisions);
        var missingItem = TransportPlanningPipeline.Plan(missingItemSnapshot);
        RegressionAssert.True(
            missingItem.ActiveDecisions.Length == 1
            && missingItem.ActiveDecisions[0].Kind == TransportActiveDecisionKind.CleanupInvalid
            && !missingItem.ActiveDecisions[0].IsAccepted,
            "Missing active authority was not expressed as deterministic cleanup.");

        Console.WriteLine("[PASS] Active decisions are exhaustive and stockpile-aware");
    }

    private static void TestPurePlannerDoesNotOwnMutableRuntimeServices()
    {
        var instanceFields = typeof(TransportPurePlanner).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var staticServiceFields = typeof(TransportPurePlanner)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(static field => !field.IsLiteral)
            .ToArray();

        RegressionAssert.True(instanceFields.Length == 0, "Pure transport planner unexpectedly owns instance state.");
        RegressionAssert.True(staticServiceFields.Length == 0, "Pure transport planner unexpectedly owns static service state.");

        Console.WriteLine("[PASS] Pure transport planner owns no mutable runtime services");
    }

    private static TransportPlanningSnapshot CreateParallelSnapshot(bool reverseInputs)
    {
        var sources = new[]
        {
            new Point3(1, 1, 0),
            new Point3(2, 1, 0),
            new Point3(3, 1, 0),
            new Point3(4, 1, 0)
        };
        var destinations = new[]
        {
            new Point3(10, 1, 0),
            new Point3(11, 1, 0),
            new Point3(12, 1, 0),
            new Point3(13, 1, 0)
        };
        int[] priorities = { 1000, 1400, 1600, 1000 };
        var requests = Enumerable.Range(0, 4)
            .Select(index => new TransportQueuedRequestReadRow(
                index,
                Request(Id(index + 1), sources[index], destinations[index], priorities[index], (ulong)index)))
            .ToArray();
        var items = Enumerable.Range(0, 4)
            .Select(index => new TransportItemReadRow(
                Id(index + 1),
                (ulong)(index + 1),
                "item.ore",
                sources[index],
                1,
                true,
                Guid.Empty))
            .ToArray();
        var creatures = Enumerable.Range(0, 4)
            .Select(index => new TransportCreatureReadRow(
                Id(index + 101),
                (ulong)(index + 101),
                new Point3(index + 1, 4, 0),
                10,
                true))
            .ToArray();
        var stockpiles = Enumerable.Range(0, 4)
            .Select(index => new TransportStockpileCellReadRow(
                index + 1,
                destinations[index],
                1,
                ImmutableArray.Create("item.ore"),
                Guid.Empty))
            .ToArray();

        if (reverseInputs)
        {
            Array.Reverse(requests);
            Array.Reverse(items);
            Array.Reverse(creatures);
            Array.Reverse(stockpiles);
        }

        return TransportPlanningSnapshot.Create(
            tick: 10,
            queuedRequests: requests,
            items: items,
            creatures: creatures,
            stockpileCells: stockpiles,
            navigationRevisions: new[]
            {
                new TransportNavigationRevisionReadRow(new ChunkKey(0, 0, 0), 7)
            });
    }

    private static TransportRequestReadRow Request(
        Guid itemId,
        Point3 source,
        Point3 destination,
        int priority,
        ulong localSequence,
        TransportReason reason = TransportReason.ToStockpile)
    {
        return new TransportRequestReadRow(
            itemId,
            source,
            destination,
            1,
            reason,
            priority,
            CreatedTick: 5,
            SystemOrder: 300,
            ProducerId: "test.transport",
            LocalSequence: localSequence,
            Seed: 1,
            PathSearchAttempt: 0);
    }

    private static TransportIntent Intent(
        int priority,
        ulong createdTick,
        int systemOrder,
        string producerId,
        Guid identity,
        ulong localSequence,
        Guid claimedItem,
        Guid creatureId)
    {
        return TransportIntent.Create(
            new TransportIntentOrderKey(
                priority,
                createdTick,
                systemOrder,
                producerId,
                identity,
                localSequence),
            TransportIntentKind.AssignRequest,
            TransportPendingSource.Queue,
            claimedItem,
            creatureId,
            new Point3(1, 1, 0),
            new Point3(2, 1, 0),
            1,
            TransportReason.Misc,
            0,
            planningWorkOrder: BitConverter.ToInt32(identity.ToByteArray(), 12),
            candidateOrder: 0,
            new TransportItemExpectation(
                EntityKey: 1,
                Position: new Point3(1, 1, 0),
                StackCount: 1,
                IsOnGround: true,
                CarrierId: Guid.Empty),
            new TransportCreatureExpectation(
                EntityKey: 1,
                Position: new Point3(0, 1, 0),
                HitPoints: 10),
            TransportStockpileExpectation.None,
            TransportPendingSplitExpectation.None,
            new TransportNavigationExpectation(
                new ChunkKey(0, 0, 0),
                StartConnectivityVersion: 1,
                new ChunkKey(0, 0, 0),
                GoalConnectivityVersion: 1),
            TransportMovementExpectation.None,
            new[] { TransportIntentResourceClaim.Item(claimedItem) });
    }

    private static IEnumerable<TransportIntent[]> Permutations(TransportIntent[] source)
    {
        for (int first = 0; first < source.Length; first++)
        {
            for (int second = 0; second < source.Length; second++)
            {
                if (second == first) continue;
                for (int third = 0; third < source.Length; third++)
                {
                    if (third == first || third == second) continue;
                    yield return new[] { source[first], source[second], source[third] };
                }
            }
        }
    }

    private static string ResolutionSignature(TransportPlanResolution resolution)
    {
        string accepted = string.Join(
            ";",
            resolution.Accepted.Select(static intent =>
                $"A:{intent.OrderKey.Priority}:{intent.OrderKey.CreatedTick}:{intent.OrderKey.SystemOrder}:{intent.OrderKey.ProducerId}:{intent.OrderKey.Identity:D}:{intent.OrderKey.LocalSequence}:{intent.Kind}:{intent.ItemId:D}:{intent.CreatureId:D}"));
        string rejected = string.Join(
            ";",
            resolution.Rejected.Select(static rejection =>
                $"R:{rejection.OrderKey.Priority}:{rejection.OrderKey.CreatedTick}:{rejection.OrderKey.SystemOrder}:{rejection.OrderKey.ProducerId}:{rejection.OrderKey.Identity:D}:{rejection.OrderKey.LocalSequence}:{rejection.Reason}:{rejection.ItemId:D}:{rejection.CreatureId:D}:{rejection.ConflictingWinner?.Identity:D}:{rejection.DetailCode}"));
        string assignmentPlans = string.Join(
            ";",
            resolution.AssignmentPlans.Select(static plan =>
                $"P:{plan.Winner.ItemId:D}:{string.Join(',', plan.OrderedCandidates.Select(static candidate => candidate.CreatureId.ToString("D")))}"));
        string activeDecisions = string.Join(
            ";",
            resolution.ActiveDecisions.Select(static decision =>
                $"D:{decision.ItemId:D}:{decision.CreatureId:D}:{decision.Kind}:{decision.IsAccepted}"));
        return $"{accepted}|{rejected}|{assignmentPlans}|{activeDecisions}";
    }

    private static async ValueTask DelayWorkersInReverseOrder(
        int workerIndex,
        CancellationToken cancellationToken)
    {
        await Task.Delay((4 - workerIndex) * 3, cancellationToken).ConfigureAwait(false);
    }

    private static Guid Id(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:x12}");
    }
}
