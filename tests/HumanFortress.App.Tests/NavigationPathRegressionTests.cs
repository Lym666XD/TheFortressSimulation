using HumanFortress.Contracts.Navigation;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Replay;
using HumanFortress.Jobs.Transport;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Save;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;
using NavPath = HumanFortress.Contracts.Navigation.Path;

internal static class NavigationPathRegressionTests
{
    internal static void RunAll()
    {
        Console.WriteLine("=== Navigation Path Regression Tests ===");
        TestWarmAndColdCacheOutcomesMatchAtEachAttempt();
        TestGoalAtNodeBudgetBoundaryIsComplete();
        TestPerTickBudgetReturnsRetryableResultWithoutHiddenQueue();
        TestMovementRejectsMalformedCompletePathAndPreservesRetryAttempt();
        TestMovementProposalDoesNotAdvanceBeforeCommit();
        TestTransportRequestAgeDoesNotIncreaseSearchAttempt();
        TestActiveRetryAttemptChangesReplayHashes();
        TestMovementCursorChangesTransportReplayHash();
        TestExperimentalSaveMappersRejectRetryState();
        TopologyTransactionRegressionTests.RunAll();
        Console.WriteLine("=== Navigation Path Regression Tests Completed ===");
        Console.WriteLine();
    }

    private static void TestWarmAndColdCacheOutcomesMatchAtEachAttempt()
    {
        var tuning = new NavigationTuning
        {
            AllowDiagonals = false,
            MaxNodesPerSearch = 1,
            MaxPathsPerTick = 8
        };
        var world = new OpenNavigationWorld(64, 64);
        var request = new PathRequest(
            new Point3(1, 1, 0),
            new Point3(20, 1, 0),
            MoveMode.Walk,
            PathFlags.None,
            1234);
        var service = new PathService(tuning);

        service.BeginTick();
        var firstPartial = service.Solve(in request, world);
        var firstStats = service.GetStats();
        service.BeginTick();
        var repeatedPartial = service.Solve(in request, world);

        var retryRequest = request;
        for (var attempt = 0; attempt < 5; attempt++)
            retryRequest = retryRequest.NextSearchAttempt();

        service.BeginTick();
        var retried = service.Solve(in retryRequest, world);
        var completedStats = service.GetStats();
        service.BeginTick();
        var warmLowAttempt = service.Solve(in request, world);
        var lowAttemptStats = service.GetStats();
        service.BeginTick();
        var warmHighAttempt = service.Solve(in retryRequest, world);
        var highAttemptStats = service.GetStats();

        var coldService = new PathService(tuning);
        coldService.BeginTick();
        var coldLowAttempt = coldService.Solve(in request, world);
        coldService.BeginTick();
        var coldHighAttempt = coldService.Solve(in retryRequest, world);

        RegressionAssert.True(
            firstPartial.Kind == PathResultKind.Partial
            && firstPartial.Steps.Length > 0
            && !firstPartial.ReachesDestination(request.Destination)
            && firstStats.CacheSize == 0
            && repeatedPartial.Kind == PathResultKind.Partial
            && repeatedPartial.Hash == firstPartial.Hash
            && repeatedPartial.Steps.Span.SequenceEqual(firstPartial.Steps.Span)
            && retried.ReachesDestination(request.Destination)
            && completedStats.CacheSize == 1
            && warmLowAttempt.Kind == PathResultKind.Partial
            && warmLowAttempt.Hash == coldLowAttempt.Hash
            && warmLowAttempt.Steps.Span.SequenceEqual(coldLowAttempt.Steps.Span)
            && lowAttemptStats.CacheHits == completedStats.CacheHits
            && warmHighAttempt.ReachesDestination(request.Destination)
            && coldHighAttempt.ReachesDestination(request.Destination)
            && warmHighAttempt.Hash == coldHighAttempt.Hash
            && warmHighAttempt.Steps.Span.SequenceEqual(coldHighAttempt.Steps.Span)
            && highAttemptStats.CacheHits > lowAttemptStats.CacheHits,
            "Warm cache changed the path result for the same deterministic search attempt.");

        Console.WriteLine("[PASS] Warm and cold cache outcomes match at each search attempt");
    }

    private static void TestGoalAtNodeBudgetBoundaryIsComplete()
    {
        var tuning = new NavigationTuning
        {
            AllowDiagonals = false,
            MaxNodesPerSearch = 1,
            MaxPathsPerTick = 1
        };
        var world = new OpenNavigationWorld(8, 8);
        var request = new PathRequest(
            new Point3(1, 1, 0),
            new Point3(2, 1, 0),
            MoveMode.Walk,
            PathFlags.None,
            99);
        var service = new PathService(tuning);

        service.BeginTick();
        var result = service.Solve(in request, world);

        RegressionAssert.True(
            result.ReachesDestination(request.Destination)
            && result.Length == 2,
            "A destination reached at the exact node budget boundary was reported as partial.");

        Console.WriteLine("[PASS] Goal at node budget boundary is complete");
    }

    private static void TestPerTickBudgetReturnsRetryableResultWithoutHiddenQueue()
    {
        var tuning = new NavigationTuning
        {
            AllowDiagonals = false,
            MaxNodesPerSearch = 100,
            MaxPathsPerTick = 1
        };
        var world = new OpenNavigationWorld(32, 32);
        var firstRequest = new PathRequest(
            new Point3(1, 1, 0),
            new Point3(4, 1, 0),
            MoveMode.Walk,
            PathFlags.None,
            1);
        var deferredRequest = new PathRequest(
            new Point3(1, 2, 0),
            new Point3(4, 2, 0),
            MoveMode.Walk,
            PathFlags.None,
            2);
        var service = new PathService(tuning);

        service.BeginTick();
        var first = service.Solve(in firstRequest, world);
        var exhausted = service.Solve(in deferredRequest, world);
        service.BeginTick();
        var retried = service.Solve(in deferredRequest, world);

        RegressionAssert.True(
            first.ReachesDestination(firstRequest.Destination)
            && exhausted.Kind == PathResultKind.BudgetExhausted
            && exhausted.Steps.IsEmpty
            && retried.ReachesDestination(deferredRequest.Destination),
            "Per-tick request exhaustion was confused with invalid input or hidden queue completion.");

        Console.WriteLine("[PASS] Per-tick budget returns retryable result without hidden queue");
    }

    private static void TestMovementRejectsMalformedCompletePathAndPreservesRetryAttempt()
    {
        var world = new OpenNavigationWorld(32, 32);
        var service = new PathService();
        var movement = new MovementExecutor(service, new NavigationTuning { MovementStepDelayTicks = 0 });
        var request = new PathRequest(
            new Point3(1, 1, 0),
            new Point3(5, 1, 0),
            MoveMode.Walk,
            PathFlags.None,
            7);
        var malformed = new NavPath(
            PathResultKind.Found,
            1,
            0,
            0,
            new[] { new PathNode(request.Source, 1) });

        bool rejectedMalformed = false;
        try
        {
            movement.BeginMovement(1, request, malformed);
        }
        catch (ArgumentException)
        {
            rejectedMalformed = true;
        }

        var partial = new NavPath(
            PathResultKind.Partial,
            1,
            0,
            0,
            new[] { new PathNode(request.Source, 1) });
        movement.BeginMovement(2, request, partial);
        var partialUpdate = movement.UpdateMovement(2, world);

        movement.BeginMovement(3, request, NavPath.BudgetExhausted);
        var exhaustedUpdate = movement.UpdateMovement(3, world);

        RegressionAssert.True(
            rejectedMalformed
            && partialUpdate is
            {
                Status: MovementStatus.NoPath,
                NeedsReplan: true,
                SearchAttempt: 1
            }
            && partialUpdate.Position == request.Source
            && exhaustedUpdate is
            {
                Status: MovementStatus.NoPath,
                NeedsReplan: true,
                SearchAttempt: 0
            }
            && exhaustedUpdate.Position == request.Source,
            "Movement accepted a malformed Found path or lost deterministic retry state.");

        Console.WriteLine("[PASS] Movement rejects malformed complete path and preserves retry attempt");
    }

    private static void TestTransportRequestAgeDoesNotIncreaseSearchAttempt()
    {
        var itemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var oldRequest = new TransportRequest(
            itemId,
            new Point(4, 5),
            0,
            new Point(8, 9),
            0,
            1,
            TransportReason.Misc,
            0,
            "path-age-test",
            CreatedTick: 0,
            Seed: 1);
        var recentRequest = oldRequest with { CreatedTick = ulong.MaxValue };
        var explicitRetry = oldRequest with { PathSearchAttempt = 3 };
        var source = new Point3(1, 2, 0);

        var oldPathRequest = TransportAssignmentHandler.CreatePathRequest(in oldRequest, source, 10);
        var recentPathRequest = TransportAssignmentHandler.CreatePathRequest(in recentRequest, source, 10);
        var retryPathRequest = TransportAssignmentHandler.CreatePathRequest(in explicitRetry, source, 10);
        var capped = retryPathRequest;
        for (var i = 0; i < 20; i++)
            capped = capped.NextSearchAttempt();

        RegressionAssert.True(
            oldPathRequest.SearchAttempt == 0
            && recentPathRequest.SearchAttempt == 0
            && retryPathRequest.SearchAttempt == 3
            && capped.SearchAttempt == PathRequest.MaxSearchAttempt,
            "Transport request age affected search budget or retry attempts exceeded their explicit cap.");

        Console.WriteLine("[PASS] Transport request age does not increase search attempt");
    }

    private static void TestMovementProposalDoesNotAdvanceBeforeCommit()
    {
        var world = new OpenNavigationWorld(16, 16);
        var movement = new MovementExecutor(
            new PathService(),
            new NavigationTuning { MovementStepDelayTicks = 0 });
        var request = new PathRequest(
            new Point3(1, 1, 0),
            new Point3(2, 1, 0),
            MoveMode.Walk,
            PathFlags.None,
            42);
        var path = new NavPath(
            PathResultKind.Found,
            2,
            10,
            99,
            new[]
            {
                new PathNode(request.Source, 0),
                new PathNode(request.Destination, 10),
            });
        movement.BeginMovement(77, request, path);

        var before = movement.GetCursorSnapshot(77);
        var rejectedProposal = movement.PlanMovement(77, world);
        var afterRejected = movement.GetCursorSnapshot(77);
        bool firstCommit = movement.TryCommitMovement(rejectedProposal);
        var afterFirstCommit = movement.GetCursorSnapshot(77);
        bool staleCommitRejected = !movement.TryCommitMovement(rejectedProposal);
        var destinationProposal = movement.PlanMovement(77, world);
        var beforeDestinationCommit = movement.GetCursorSnapshot(77);
        bool destinationCommit = movement.TryCommitMovement(destinationProposal);
        var afterDestinationCommit = movement.GetCursorSnapshot(77);

        RegressionAssert.True(
            before.HasValue
            && afterRejected.HasValue
            && afterRejected.Value.Revision == before.Value.Revision
            && afterRejected.Value.CurrentStep == before.Value.CurrentStep
            && afterRejected.Value.Position == before.Value.Position
            && afterRejected.Value.Path.Steps.Span.SequenceEqual(before.Value.Path.Steps.Span)
            && rejectedProposal.NextCursor?.CurrentStep == 1
            && firstCommit
            && afterFirstCommit?.CurrentStep == 1
            && staleCommitRejected
            && beforeDestinationCommit?.Position == request.Source
            && destinationProposal.Update.Position == request.Destination
            && destinationCommit
            && afterDestinationCommit?.Position == request.Destination
            && afterDestinationCommit?.CurrentStep == 2,
            "Rejected movement planning advanced cursor authority before commit or stale CAS succeeded.");

        Console.WriteLine("[PASS] Movement proposals advance only after revision-CAS commit");
    }

    private static void TestActiveRetryAttemptChangesReplayHashes()
    {
        var craftAtZero = CreateCraftSnapshot(0);
        var craftAtOne = CreateCraftSnapshot(1);
        var miningAtZero = CreateMiningSnapshot(0);
        var miningAtOne = CreateMiningSnapshot(1);
        var transportAtZero = CreateTransportSnapshot(0);
        var transportAtOne = CreateTransportSnapshot(1);
        var emptyTransportQueue = new TransportRequestQueueStateSnapshot(
            Array.Empty<TransportRequest>());

        RegressionAssert.True(
            CraftReplayHashBuilder.Build(craftAtZero) != CraftReplayHashBuilder.Build(craftAtOne)
            && MiningReplayHashBuilder.Build(miningAtZero) != MiningReplayHashBuilder.Build(miningAtOne)
            && TransportReplayHashBuilder.Build(emptyTransportQueue, transportAtZero)
                != TransportReplayHashBuilder.Build(emptyTransportQueue, transportAtOne),
            "Active path retry state did not contribute to every job replay hash.");

        Console.WriteLine("[PASS] Active retry attempt changes job replay hashes");
    }

    private static void TestExperimentalSaveMappersRejectRetryState()
    {
        var emptyTransportQueue = new TransportRequestQueueStateSnapshot(
            Array.Empty<TransportRequest>());

        RegressionAssert.True(
            ThrowsNotSupported(() => RuntimeSaveSnapshotDocumentCraftMapper.ToDocumentData(CreateCraftSnapshot(1)))
            && ThrowsNotSupported(() => RuntimeSaveSnapshotDocumentMiningMapper.ToDocumentData(CreateMiningSnapshot(1)))
            && ThrowsNotSupported(() => RuntimeSaveSnapshotDocumentTransportMapper.ToDocumentData(
                emptyTransportQueue,
                CreateTransportSnapshot(1))),
            "An experimental save mapper silently discarded non-zero path retry state.");

        Console.WriteLine("[PASS] Experimental save mappers reject retry state they cannot encode");
    }

    private static void TestMovementCursorChangesTransportReplayHash()
    {
        var queue = new TransportRequestQueueStateSnapshot(Array.Empty<TransportRequest>());
        var first = CreateTransportSnapshot(0);
        var active = first.ActiveJobs[0];
        var request = new PathRequest(
            new Point3(1, 1, 0),
            new Point3(2, 1, 0),
            MoveMode.Walk,
            PathFlags.None,
            5);
        var path = new NavPath(
            PathResultKind.Found,
            2,
            10,
            7,
            new[]
            {
                new PathNode(request.Source, 0),
                new PathNode(request.Destination, 10),
            });
        var before = first with
        {
            ActiveJobs = new[]
            {
                active with
                {
                    MovementCursor = new MovementCursorData(
                        10,
                        1,
                        request,
                        path,
                        CurrentStep: 0,
                        Position: request.Source,
                        StuckTicks: 0,
                        LastProgress: 0,
                        LastConnectivityVersion: 0,
                        StepWait: 0),
                },
            },
        };
        var after = before with
        {
            ActiveJobs = new[]
            {
                before.ActiveJobs[0] with
                {
                    MovementCursor = before.ActiveJobs[0].MovementCursor!.Value with
                    {
                        Revision = 2,
                        CurrentStep = 1,
                    },
                },
            },
        };

        RegressionAssert.True(
            TransportReplayHashBuilder.Build(queue, before)
                != TransportReplayHashBuilder.Build(queue, after),
            "Transport replay hash ignored movement cursor revision/progress authority.");

        Console.WriteLine("[PASS] Movement cursor changes transport replay hash");
    }

    private static CraftJobReplaySnapshot CreateCraftSnapshot(byte searchAttempt)
    {
        var active = new CraftActiveJobStateSnapshot(
            0,
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            Guid.Parse("20000000-0000-0000-0000-000000000003"),
            "path_test_recipe",
            CraftJobStage.ToWorkshop,
            5,
            new Point(2, 3),
            0,
            searchAttempt);
        return new CraftJobReplaySnapshot(
            new[] { active },
            Array.Empty<CraftBacklogEntrySnapshot>());
    }

    private static MiningJobReplaySnapshot CreateMiningSnapshot(byte searchAttempt)
    {
        var active = new MiningActiveJobStateSnapshot(
            0,
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            new Point(4, 4),
            0,
            new Point(4, 3),
            MiningStage.ToAdj,
            0,
            5,
            1,
            TerrainKind.SolidWall,
            0,
            0,
            0,
            MiningAction.Dig,
            MiningSegment.None,
            1,
            searchAttempt);
        return new MiningJobReplaySnapshot(
            new[] { active },
            Array.Empty<MiningBacklogEntrySnapshot>(),
            Array.Empty<MiningDeferredStairwellSnapshot>(),
            Array.Empty<MiningReservedTileSnapshot>(),
            Array.Empty<MiningRecentCompletionSnapshot>());
    }

    private static TransportJobReplaySnapshot CreateTransportSnapshot(byte searchAttempt)
    {
        var active = new TransportActiveJobStateSnapshot(
            0,
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Guid.Parse("40000000-0000-0000-0000-000000000002"),
            new Point3(6, 7, 0),
            JobStage.ToItem,
            1,
            0,
            TransportReason.Misc,
            searchAttempt);
        return new TransportJobReplaySnapshot(
            null,
            null,
            0,
            new[] { active },
            Array.Empty<TransportBacklogEntrySnapshot>());
    }

    private static bool ThrowsNotSupported(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (NotSupportedException)
        {
            return true;
        }
    }

    private sealed class OpenNavigationWorld : IWorldNavigationView
    {
        private readonly int _width;
        private readonly int _height;

        internal OpenNavigationWorld(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public bool IsValid(Point3 position)
        {
            return position.X >= 0
                && position.X < _width
                && position.Y >= 0
                && position.Y < _height
                && position.Z == 0;
        }

        public NavCapability GetCapabilities(Point3 position)
        {
            return IsValid(position)
                ? NavCapability.Walk | NavCapability.Standable
                : NavCapability.None;
        }

        public ushort GetCost(Point3 position)
        {
            return IsValid(position) ? (ushort)1 : ushort.MaxValue;
        }

        public bool IsWalkable(Point3 position, MoveMode mode)
        {
            return IsValid(position) && mode == MoveMode.Walk;
        }

        public bool HasStairsUp(Point3 position) => false;

        public bool HasStairsDown(Point3 position) => false;

        public int GetConnectivityVersion(ChunkKey chunk) => 1;
    }
}
