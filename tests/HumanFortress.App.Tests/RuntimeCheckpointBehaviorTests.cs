using System.Reflection;
using HumanFortress.Contracts.Runtime.Checkpoints;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Core.Determinism;
using HumanFortress.Jobs.Mining;
using HumanFortress.Runtime;
using HumanFortress.Runtime.Checkpoints;
using HumanFortress.Runtime.Replay;
using HumanFortress.Runtime.Session;
using HumanFortress.Runtime.Snapshots;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

internal static class RuntimeCheckpointBehaviorTests
{
    internal static void RunAll()
    {
        Console.WriteLine("=== Runtime Checkpoint Behavior Tests ===");
        TestSectionOrderDoesNotChangeCanonicalIdentity();
        TestPublicationOwnsInputsAndOldReferencesRemainImmutable();
        TestGenerationFenceRejectsLatePublication();
        TestRetentionAndFullRecoverySemantics();
        TestCommittedOwnerOwnsGenerationRetentionAndDiagnosticsCopies();
        TestRuntimePublishesReplayAtTheCommittedPostTickBoundary();
        TestCheckpointProjectorsDoNotMutateReplayAuthority();
        RuntimeAppFrameBehaviorTests.RunAll();
        Console.WriteLine("=== Runtime Checkpoint Behavior Tests Completed ===");
        Console.WriteLine();
    }

    private static void TestSectionOrderDoesNotChangeCanonicalIdentity()
    {
        var generation = new RuntimeCheckpointGenerationLease(7);
        var content = ContentIdentity();
        var first = new RuntimeCommittedCheckpoint(
            generation,
            runtimeTick: 42,
            content: content,
            sections: new[]
            {
                Section("world", 1, 2, 3),
                Section("commands", 4, 5),
            });
        var second = new RuntimeCommittedCheckpoint(
            generation,
            runtimeTick: 42,
            content: content,
            sections: new[]
            {
                Section("commands", 4, 5),
                Section("world", 1, 2, 3),
            });

        RegressionAssert.True(
            first.Identity.AggregateHash == second.Identity.AggregateHash
            && first.Identity.HashAlgorithm == ReplayHashBuilder.Algorithm
            && first.Identity.Sections.Select(static section => section.SectionId)
                .SequenceEqual(new[] { "commands", "world" }, StringComparer.Ordinal)
            && second.Identity.Sections.Select(static section => section.SectionId)
                .SequenceEqual(new[] { "commands", "world" }, StringComparer.Ordinal),
            "Checkpoint identity depended on section input order.");

        Console.WriteLine("[PASS] Checkpoint section identity is canonically ordered");
    }

    private static void TestPublicationOwnsInputsAndOldReferencesRemainImmutable()
    {
        var coordinator = new RuntimeCheckpointCoordinator();
        var generation = coordinator.BeginGeneration();
        var payload = new byte[] { 1, 2, 3 };
        var inputs = new List<RuntimeCheckpointSectionInput>
        {
            new("world", 1, payload),
        };

        RegressionAssert.True(
            coordinator.TryPublish(generation, 1, ContentIdentity(), inputs, out var checkpoint)
            && checkpoint != null,
            "Initial checkpoint publication failed.");
        string aggregateHash = checkpoint!.Identity.AggregateHash;

        payload[0] = 99;
        inputs.Clear();
        RegressionAssert.True(
            checkpoint.TryCopySectionPayload("world", out var firstCopy)
            && firstCopy.SequenceEqual(new byte[] { 1, 2, 3 }),
            "Checkpoint retained a mutable publication input.");

        firstCopy[1] = 88;
        RegressionAssert.True(
            checkpoint.TryCopySectionPayload("world", out var secondCopy)
            && secondCopy.SequenceEqual(new byte[] { 1, 2, 3 })
            && checkpoint.Identity.AggregateHash == aggregateHash,
            "Checkpoint exposed mutable payload storage or changed an old identity.");

        Console.WriteLine("[PASS] Checkpoint publication owns immutable input copies");
    }

    private static void TestGenerationFenceRejectsLatePublication()
    {
        var coordinator = new RuntimeCheckpointCoordinator();
        var firstGeneration = coordinator.BeginGeneration();
        RegressionAssert.True(
            coordinator.TryPublish(
                firstGeneration,
                1,
                ContentIdentity(),
                new[] { Section("world", 1) },
                out _),
            "First generation checkpoint publication failed.");

        var secondGeneration = coordinator.BeginGeneration();
        RegressionAssert.True(
            !firstGeneration.IsValid
            && coordinator.Store.RetainedCount == 0
            && !coordinator.TryPublish(
                firstGeneration,
                2,
                ContentIdentity(),
                new[] { Section("world", 2) },
                out _)
            && coordinator.TryPublish(
                secondGeneration,
                1,
                ContentIdentity(),
                new[] { Section("world", 3) },
                out var current)
            && current?.Identity.SessionGeneration == secondGeneration.Generation,
            "A stale generation published into the active checkpoint store.");

        Console.WriteLine("[PASS] Checkpoint generation fence rejects late publication");
    }

    private static void TestRetentionAndFullRecoverySemantics()
    {
        var coordinator = new RuntimeCheckpointCoordinator();
        var generation = coordinator.BeginGeneration();
        var published = new List<RuntimeCommittedCheckpoint>();
        for (ulong tick = 1; tick <= 4; tick++)
        {
            RegressionAssert.True(
                coordinator.TryPublish(
                    generation,
                    tick,
                    ContentIdentity(),
                    new[] { Section("world", (byte)tick) },
                    out var checkpoint)
                && checkpoint != null,
                $"Checkpoint publication failed at tick {tick}.");
            published.Add(checkpoint!);
        }

        var retainedBase = coordinator.ResolvePublication(
            requestHash: "map-request",
            previousRequestHash: "map-request",
            requestedBaseAggregateHash: published[1].Identity.AggregateHash);
        var lostBase = coordinator.ResolvePublication(
            requestHash: "map-request",
            previousRequestHash: "map-request",
            requestedBaseAggregateHash: published[0].Identity.AggregateHash);
        var requestMismatch = coordinator.ResolvePublication(
            requestHash: "map-request-v2",
            previousRequestHash: "map-request",
            requestedBaseAggregateHash: published[2].Identity.AggregateHash);

        RegressionAssert.True(
            coordinator.Store.RetainedCount == RuntimeCheckpointStore.MaximumRetainedCheckpointCount
            && retainedBase.Mode == RuntimeCheckpointPublicationMode.Delta
            && retainedBase.BaseCheckpoint?.Identity.RuntimeTick == 2
            && retainedBase.Checkpoint?.Identity.RuntimeTick == 4
            && lostBase.RequiresFullSnapshot
            && lostBase.BaseCheckpoint == null
            && lostBase.Checkpoint?.Identity.RuntimeTick == 4
            && requestMismatch.RequiresFullSnapshot
            && requestMismatch.BaseCheckpoint == null,
            "Checkpoint retention or full-recovery behavior was incorrect.");

        Console.WriteLine("[PASS] Checkpoint retention is bounded and base loss recovers with Full");
    }

    private static RuntimeContentIdentityData ContentIdentity()
    {
        return new RuntimeContentIdentityData(
            SchemaVersion: 1,
            Signature: "test-content",
            MechanicalHash: "test-mechanical-hash",
            HashAlgorithm: ReplayHashBuilder.Algorithm);
    }

    private static void TestCommittedOwnerOwnsGenerationRetentionAndDiagnosticsCopies()
    {
        var owner = new RuntimeCommittedCheckpointOwner();
        var services = new RuntimeSessionServices();
        var firstGeneration = owner.ActivateGeneration(content: null);
        var published = new List<RuntimeCheckpointIdentityData>();
        for (ulong tick = 1; tick <= 4; tick++)
        {
            RegressionAssert.True(
                owner.TryPublishCommitted(
                    firstGeneration,
                    services,
                    session: null,
                    systems: null,
                    tick,
                    appFrames: null,
                    out var identity),
                $"Committed checkpoint owner failed to publish tick {tick}.");
            published.Add(identity);
        }

        RegressionAssert.True(
            owner.TryGetLatestDiagnostics(out var diagnostics)
            && diagnostics.CheckpointIdentity.RuntimeTick == 4
            && diagnostics.Facts.Metadata.RuntimeTick == 4
            && diagnostics.Facts.RngStreamCount >= 0
            && diagnostics.CheckpointIdentity.Sections
                .Select(static section => section.SectionId)
                .SequenceEqual(
                    new[] { "jobs.professions", "runtime-diagnostics", "runtime-replay" },
                    StringComparer.Ordinal),
            "Committed checkpoint owner did not expose same-tick diagnostics facts.");

        var firstSections = diagnostics.CheckpointIdentity.Sections;
        RegressionAssert.True(
            owner.TryGetLatestDiagnostics(out var freshDiagnostics)
            && !ReferenceEquals(
                firstSections,
                freshDiagnostics.CheckpointIdentity.Sections),
            "Diagnostics reads reused a consumer-visible checkpoint identity collection.");
        bool mutationRejected = false;
        if (firstSections is IList<RuntimeCheckpointSectionIdentityData> exposed)
        {
            try
            {
                exposed[0] = exposed[0] with { SectionId = "consumer-mutation" };
            }
            catch (NotSupportedException)
            {
                mutationRejected = true;
            }
        }

        var retained = owner.ResolvePublication(
            "owner-request",
            "owner-request",
            published[1].AggregateHash);
        var lost = owner.ResolvePublication(
            "owner-request",
            "owner-request",
            published[0].AggregateHash);
        var secondGeneration = owner.ActivateGeneration(content: null);
        RegressionAssert.True(
            mutationRejected
            && owner.RetainedCount == 0
            && !firstGeneration.IsValid
            && retained.Mode == RuntimeCheckpointPublicationMode.Delta
            && lost.RequiresFullSnapshot
            && !owner.TryPublishCommitted(
                firstGeneration,
                services,
                session: null,
                systems: null,
                committedTick: 5,
                appFrames: null,
                out _)
            && owner.TryPublishCommitted(
                secondGeneration,
                services,
                session: null,
                systems: null,
                committedTick: 1,
                appFrames: null,
                out var replacement)
            && replacement.SessionGeneration == secondGeneration.Generation
            && ReferenceEquals(owner.InvalidateActiveGeneration(), secondGeneration)
            && !owner.TryGetLatestIdentity(out _),
            "Committed checkpoint owner did not own generation invalidation or retention.");

        Console.WriteLine("[PASS] Committed checkpoint owner owns generation, retention, and diagnostics copies");
    }

    private static void TestRuntimePublishesReplayAtTheCommittedPostTickBoundary()
    {
        var runtime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        runtime.InitializeWorld(sizeInChunks: 2, maxZ: 2);

        RegressionAssert.True(
            !runtime.TryGetLatestCheckpointIdentity(out _),
            "A world that had not committed a tick exposed a committed checkpoint.");

        runtime.StartFortressPlay(enqueueAutoDig: false);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!runtime.TryGetLatestCheckpointIdentity(out _) && DateTime.UtcNow < deadline)
            Thread.Sleep(5);
        runtime.StopIfRunning();

        var replay = runtime.GetReplayCheckpointData();
        RegressionAssert.True(
            runtime.TryGetLatestCheckpointIdentity(out var identity)
            && runtime.TryGetLatestCommittedDiagnostics(out var diagnostics)
            && identity.SessionGeneration == 1
            && identity.RuntimeTick == replay.Metadata.RuntimeTick
            && diagnostics.CheckpointIdentity.SessionGeneration == identity.SessionGeneration
            && diagnostics.CheckpointIdentity.RuntimeTick == identity.RuntimeTick
            && diagnostics.CheckpointIdentity.AggregateHash == identity.AggregateHash
            && diagnostics.Facts.Metadata.RuntimeTick == replay.Metadata.RuntimeTick
            && diagnostics.Facts.RngStreamCount == replay.RngStreamCount
            && diagnostics.Facts.ExecutedCommandCount == replay.CommandLogRecordCount
            && diagnostics.Facts.PendingCommandCount == replay.PendingCommandLogRecordCount
            && identity.Sections.Select(static section => section.SectionId)
                .SequenceEqual(
                    new[] { "jobs.professions", "runtime-diagnostics", "runtime-replay" },
                    StringComparer.Ordinal),
            "Runtime replay was not published from the atomic committed PostTick checkpoint.");

        ulong firstGeneration = identity.SessionGeneration;
        runtime.InitializeWorld(sizeInChunks: 2, maxZ: 2);
        RegressionAssert.True(
            !runtime.TryGetLatestCheckpointIdentity(out _),
            "Replacing a session retained a checkpoint from the previous generation.");
        runtime.StartFortressPlay(enqueueAutoDig: false);
        deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!runtime.TryGetLatestCheckpointIdentity(out _) && DateTime.UtcNow < deadline)
            Thread.Sleep(5);
        runtime.StopIfRunning();

        RegressionAssert.True(
            runtime.TryGetLatestCheckpointIdentity(out var replacement)
            && replacement.SessionGeneration > firstGeneration,
            "Replacement session did not publish under a new checkpoint generation.");

        Console.WriteLine("[PASS] Runtime publishes replay at the committed PostTick boundary");
    }

    private static void TestCheckpointProjectorsDoNotMutateReplayAuthority()
    {
        var core = new FortressRuntimeSessionCore(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var runtimeLifecycle = (IFortressRuntimeSessionLifecyclePort)core;
        runtimeLifecycle.InitializeWorld(sizeInChunks: 2, maxZ: 2);
        var lifecycle = (RuntimeSessionLifecycleOwner)(typeof(FortressRuntimeSessionCore)
            .GetField("_lifecycle", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(core)
            ?? throw new InvalidOperationException("Missing Runtime lifecycle owner."));
        var session = lifecycle.ActiveSession
            ?? throw new InvalidOperationException("Missing initialized Runtime session.");
        runtimeLifecycle.StartFortressPlay(enqueueAutoDig: false);
        RegressionAssert.True(
            SpinWait.SpinUntil(
                () => lifecycle.Services.TickScheduler.CurrentTick > 0,
                millisecondsTimeout: 2000),
            "Projection-purity test did not configure and advance Runtime systems.");
        RegressionAssert.True(
            runtimeLifecycle.Stop(TimeSpan.FromSeconds(1)).HasStopped,
            "Projection-purity test could not stop the configured Runtime session.");
        var systems = session.Host.RequireSystems();
        session.World.GetOrCreateChunk(new ChunkKey(0, 0, 0));
        var workerId = Guid.Parse("13572468-2468-1357-2468-135724681357");
        var creatureRestoreIssues = session.World.Creatures.RestoreCreaturesSnapshot(
            new[]
            {
                new WorldSaveCreaturePayloadData(
                    workerId,
                    "projection_test_worker",
                    "player",
                    new WorldSavePointData(1, 1),
                    0,
                    HP: 100,
                    MaxHP: 100,
                    SpawnedAtTick: 0),
            });
        var miningRestore = systems.MiningJobs.RestoreReplaySnapshot(
            new MiningJobReplaySnapshot(
                Array.Empty<MiningActiveJobStateSnapshot>(),
                Array.Empty<MiningBacklogEntrySnapshot>(),
                Array.Empty<MiningDeferredStairwellSnapshot>(),
                Array.Empty<MiningReservedTileSnapshot>(),
                new[]
                {
                    new MiningRecentCompletionSnapshot(
                        0,
                        new Point(2, 2),
                        0,
                        ExpireTick: 1),
                }));
        var before = RuntimeCommittedReplayHashBuilder.Build(
            RuntimeReplayCheckpointHashBuilder.BuildData(lifecycle.Services, session),
            systems);

        var jobs = JobsDebugSnapshotBuilder.Build(session.Host, tick: 1);
        var workforce = WorkforceSnapshotBuilder.Build(session.Host, session.World);
        var after = RuntimeCommittedReplayHashBuilder.Build(
            RuntimeReplayCheckpointHashBuilder.BuildData(lifecycle.Services, session),
            systems);

        var checks = new (string Name, bool Passed)[]
        {
            ("creature-restore", creatureRestoreIssues.Count == 0),
            ("mining-restore", miningRestore.Success),
            ("jobs-projection", jobs.HasValue),
            ("expired-completion-filter", jobs.HasValue && jobs.Value.RecentMiningCompletions.Count == 0),
            ("workforce-projection", workforce.Roster.Any(entry => entry.WorkerId == workerId)),
            ("committed-aggregate", before.Replay.AggregateHash == after.Replay.AggregateHash),
            ("mining-hash", before.Replay.MiningHash == after.Replay.MiningHash),
            ("profession-section", before.Professions == after.Professions),
        };
        RegressionAssert.True(
            checks.All(static check => check.Passed),
            "Checkpoint projection mutated replay authority or failed setup: "
            + string.Join(", ", checks.Where(static check => !check.Passed).Select(static check => check.Name)));

        ((IDisposable)core).Dispose();
        Console.WriteLine("[PASS] Checkpoint projectors do not mutate replay authority");
    }

    private static RuntimeCheckpointSectionInput Section(string id, params byte[] payload)
    {
        return new RuntimeCheckpointSectionInput(id, SchemaVersion: 1, Payload: payload);
    }
}
