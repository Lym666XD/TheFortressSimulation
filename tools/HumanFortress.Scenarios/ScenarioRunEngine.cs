using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Checkpoints;
using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Runtime;
using HumanFortress.Runtime.Diagnostics;

namespace HumanFortress.Scenarios;

internal sealed record ScenarioRunOptions(
    string BaseDirectory,
    int TransportPlanningWorkers,
    bool PrimeDerivedCaches,
    int ForceGcEveryTicks,
    bool ProcessWarm);

internal static class ScenarioRunEngine
{
    internal static ScenarioRunArtifact Run(
        LoadedScenarioInputs inputs,
        ScenarioRunOptions options)
    {
        ValidateOptions(options);
        if (options.ProcessWarm)
            _ = RunOnce(inputs, options with { ProcessWarm = false }, capturePerformance: false);

        return RunOnce(inputs, options, capturePerformance: true);
    }

    internal static void WriteArtifact(string path, ScenarioRunArtifact artifact)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + $".{Environment.ProcessId}.tmp";
        try
        {
            File.WriteAllBytes(
                temporaryPath,
                JsonSerializer.SerializeToUtf8Bytes(artifact, ScenarioJson.Strict));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static ScenarioRunArtifact RunOnce(
        LoadedScenarioInputs inputs,
        ScenarioRunOptions options,
        bool capturePerformance)
    {
        var profile = inputs.Profile;
        var totalTicks = checked(profile.WarmupTicks + profile.MeasuredTicks);
        foreach (var record in inputs.CommandRecords)
        {
            if (record.Tick >= (ulong)totalTicks)
            {
                throw new InvalidDataException(
                    $"Command '{record.CommandId}' is scheduled at tick {record.Tick}, "
                    + $"outside scenario range 0..{totalTicks - 1}.");
            }
        }

        using var session = FortressRuntimeSessionFactory.CreateHeadlessScenario(
            options.BaseDirectory,
            profile.RuntimeSeed,
            options.TransportPlanningWorkers,
            strictContent: true,
            contentWarningsAsErrors: true);
        session.InitializeWorld(profile.World.SizeInChunks, profile.World.MaxZ);
        PrepareWorld(session, profile);
        session.ConfigureManualTicks(profile.Workload.InitialCreatures);
        var workload = session.SeedWorkload(new RuntimeHeadlessWorkloadRequest(
            profile.Workload.ItemDefinitionId,
            profile.Workload.ItemInstances,
            profile.Workload.TransportRequests,
            profile.World.StandableZ));
        var restore = session.RestoreCommandJournal(inputs.CommandRecords);
        if (!restore.Success || restore.RestoredCommandCount != inputs.CommandRecords.Count)
        {
            var issues = string.Join(
                "; ",
                restore.Issues.Select(static issue => issue.Message));
            throw new InvalidDataException(
                $"Command journal restore failed ({restore.RestoredCommandCount}/{inputs.CommandRecords.Count}): {issues}");
        }

        ScenarioCachePrimeEvidence? cachePrime = null;
        if (options.PrimeDerivedCaches)
        {
            var prime = session.PrimeDerivedCaches();
            cachePrime = new ScenarioCachePrimeEvidence(
                prime.RequestsIssued,
                prime.CompletePaths,
                prime.CacheHitsAdded);
        }

        var initialMetrics = session.CaptureHeadlessMetrics();
        if (initialMetrics.CreatureCountCurrent < profile.Workload.InitialCreatures)
        {
            throw new InvalidOperationException(
                $"Scenario creature count is below its declaration: "
                + $"{initialMetrics.CreatureCountCurrent}/{profile.Workload.InitialCreatures}.");
        }
        if (initialMetrics.Planners.TransportPlanningWorkerCountConfigured
            != options.TransportPlanningWorkers)
        {
            throw new InvalidOperationException(
                "Runtime did not compose the requested transport planner worker count.");
        }

        var replayCheckpoints = new List<ScenarioReplayCheckpointEvidence>();
        var tickTimes = new List<long>(profile.MeasuredTicks);
        var tickAllocations = new List<long>(profile.MeasuredTicks);
        var counters = new ScenarioCounterAccumulator();
        var peakWorkingSet = Process.GetCurrentProcess().WorkingSet64;
        var gc0Before = GC.CollectionCount(0);
        var gc1Before = GC.CollectionCount(1);
        var gc2Before = GC.CollectionCount(2);

        RuntimeHeadlessMetricsSnapshot metrics = initialMetrics;
        for (var index = 0; index < totalTicks; index++)
        {
            var allocationBefore = GC.GetTotalAllocatedBytes(precise: false);
            var started = Stopwatch.GetTimestamp();
            session.ExecuteSingleTick();
            var elapsed = Stopwatch.GetTimestamp() - started;
            var allocated = GC.GetTotalAllocatedBytes(precise: false) - allocationBefore;

            metrics = session.CaptureHeadlessMetrics();
            EnsureHealthy(metrics);
            counters.Add(metrics);
            peakWorkingSet = Math.Max(
                peakWorkingSet,
                Process.GetCurrentProcess().WorkingSet64);

            if (index >= profile.WarmupTicks && capturePerformance)
            {
                tickTimes.Add(ToMicroseconds(elapsed));
                tickAllocations.Add(Math.Max(0, allocated));
            }

            var completedTicks = index + 1;
            if (completedTicks % profile.CheckpointInterval == 0
                || completedTicks == totalTicks)
            {
                AddReplayCheckpoint(
                    replayCheckpoints,
                    session.GetReplayCheckpointData());
            }

            if (options.ForceGcEveryTicks > 0
                && completedTicks % options.ForceGcEveryTicks == 0)
            {
                ApplyGcPressure();
            }
        }

        if (!metrics.Paths.InstrumentationIsComplete)
            throw new InvalidOperationException("Scenario path instrumentation is incomplete.");
        if (!metrics.Checkpoints.IsAvailable)
            throw new InvalidOperationException("Scenario did not publish a committed checkpoint.");
        if (!session.TryGetLatestCheckpointIdentity(out var checkpointIdentity))
            throw new InvalidOperationException("Scenario checkpoint identity is unavailable.");

        var identity = new ScenarioArtifactIdentity(
            profile.Id,
            inputs.ProfileHash,
            ScenarioInputLoader.BuildScenarioHash(inputs, totalTicks),
            inputs.Journal.Id,
            inputs.Journal.JournalHash,
            checkpointIdentity.HashAlgorithm);
        var deterministic = BuildDeterministicEvidence(
            profile,
            workload,
            inputs.CommandRecords.Count,
            checkpointIdentity,
            replayCheckpoints,
            metrics,
            counters);
        var performance = BuildPerformanceEvidence(
            tickTimes,
            tickAllocations,
            peakWorkingSet,
            gc0Before,
            gc1Before,
            gc2Before,
            metrics,
            cachePrime);

        return new ScenarioRunArtifact(
            SchemaVersion: 1,
            identity,
            new ScenarioVariantEvidence(
                options.TransportPlanningWorkers,
                options.PrimeDerivedCaches,
                options.ForceGcEveryTicks,
                options.ProcessWarm,
                Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "default"),
            deterministic,
            performance);
    }

    private static void PrepareWorld(
        IFortressRuntimeHeadlessScenarioSessionPorts session,
        ScenarioProfileDocument profile)
    {
        if (profile.World.Mode == "flat")
        {
            session.FillDeterministicFlatWorld(profile.World.StandableZ);
            return;
        }

        var generation = session.GenerateAndFillFortressWorld(
            new RuntimeFortressGenerationRequest(
                profile.World.SizeInChunks,
                EmbarkX: 17,
                EmbarkY: 29,
                BiomeId: 1,
                Elevation: 0.55f,
                Temperature: 0.45f,
                Rainfall: 0.5f,
                Drainage: 0.5f,
                RiverClass: 0,
                HasAquifer: false,
                StoneSet: Array.Empty<ushort>(),
                LandmarkIds: Array.Empty<int>(),
                GenerationSeed: profile.World.GenerationSeed));
        if (!generation.Succeeded
            || generation.FortressMapSize != profile.World.SizeInChunks)
        {
            throw new InvalidOperationException(
                $"Fortress generation failed with status '{generation.Status}'.");
        }
    }

    private static ScenarioDeterministicEvidence BuildDeterministicEvidence(
        ScenarioProfileDocument profile,
        RuntimeHeadlessWorkloadResult workload,
        int journalRecordCount,
        RuntimeCheckpointIdentityData checkpointIdentity,
        IReadOnlyList<ScenarioReplayCheckpointEvidence> replayCheckpoints,
        RuntimeHeadlessMetricsSnapshot metrics,
        ScenarioCounterAccumulator counters)
    {
        return new ScenarioDeterministicEvidence(
            profile.RuntimeSeed,
            profile.World.GenerationSeed,
            checked(profile.WarmupTicks + profile.MeasuredTicks),
            checkpointIdentity.Content.Signature,
            checkpointIdentity.Content.MechanicalHash,
            new ScenarioInitialAuthorityEvidence(
                profile.Workload.InitialCreatures,
                workload.ItemInstancesSeeded,
                workload.TransportRequestsSeeded,
                journalRecordCount),
            replayCheckpoints,
            new ScenarioFinalAuthorityEvidence(
                metrics.CreatureCountCurrent,
                metrics.ItemInstanceCountCurrent,
                metrics.Planners.TransportPendingCurrent,
                metrics.Planners.TransportActiveCurrent,
                metrics.Planners.TransportBacklogCurrent,
                metrics.Planners.MiningActiveCurrent,
                metrics.Planners.MiningBacklogCurrent,
                metrics.Planners.CraftActiveCurrent,
                metrics.Planners.CraftBacklogCurrent,
                metrics.Topology.DirtyChunksProcessedTotal,
                metrics.Topology.NavigationChunkRebuildsTotal,
                metrics.SchedulerHealth.SystemFailureCountTotal,
                metrics.SchedulerHealth.QuarantinedSystemCountCurrent),
            counters.ToEvidence());
    }

    private static ScenarioPerformanceEvidence BuildPerformanceEvidence(
        IReadOnlyList<long> tickTimes,
        IReadOnlyList<long> tickAllocations,
        long peakWorkingSet,
        int gc0Before,
        int gc1Before,
        int gc2Before,
        RuntimeHeadlessMetricsSnapshot metrics,
        ScenarioCachePrimeEvidence? cachePrime)
    {
        return new ScenarioPerformanceEvidence(
            new ScenarioEnvironmentEvidence(
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                RuntimeInformation.FrameworkDescription,
                GCSettings.IsServerGC ? "server" : "workstation",
                Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "default",
                Environment.ProcessorCount),
            Distribution(tickTimes),
            Distribution(tickAllocations),
            tickAllocations.Sum(),
            peakWorkingSet,
            GC.CollectionCount(0) - gc0Before,
            GC.CollectionCount(1) - gc1Before,
            GC.CollectionCount(2) - gc2Before,
            metrics.Paths.CacheHitsTotal,
            metrics.Paths.CacheMissesTotal,
            metrics.Paths.CacheEntriesCurrent,
            metrics.Paths.InstrumentationIsComplete,
            metrics.Checkpoints.PayloadBytesCurrent,
            metrics.Checkpoints.SectionCountCurrent,
            metrics.Checkpoints.RetainedCheckpointCountCurrent,
            cachePrime,
            tickTimes.ToArray(),
            tickAllocations.ToArray());
    }

    private static void EnsureHealthy(RuntimeHeadlessMetricsSnapshot metrics)
    {
        if (!metrics.SchedulerHealth.HasAnySystemFailure)
            return;

        var failures = string.Join(
            ", ",
            metrics.SchedulerHealth.SystemsWithFailures.Select(static failure =>
                $"{failure.SystemId}:total={failure.FailureCountTotal},"
                + $"consecutive={failure.ConsecutiveFailureCountCurrent},"
                + $"quarantined={failure.IsQuarantinedCurrent}"));
        throw new InvalidOperationException(
            $"Scenario scheduler reported a system failure: {failures}");
    }

    private static void AddReplayCheckpoint(
        ICollection<ScenarioReplayCheckpointEvidence> target,
        RuntimeReplayCheckpointData replay)
    {
        target.Add(new ScenarioReplayCheckpointEvidence(
            replay.Metadata.RuntimeTick,
            replay.AggregateHash,
            replay.WorldHash,
            replay.RngHash,
            replay.CommandLogHash,
            replay.PendingCommandLogHash,
            replay.TransportHash,
            replay.MiningHash,
            replay.CraftHash));
    }

    private static ScenarioDistributionEvidence Distribution(IReadOnlyList<long> values)
    {
        if (values.Count == 0)
            return new ScenarioDistributionEvidence(0, 0, 0, 0, 0);

        var sorted = values.OrderBy(static value => value).ToArray();
        return new ScenarioDistributionEvidence(
            sorted.Length,
            Percentile(sorted, 0.50),
            Percentile(sorted, 0.95),
            Percentile(sorted, 0.99),
            sorted[^1]);
    }

    private static long Percentile(IReadOnlyList<long> sorted, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static long ToMicroseconds(long timestampDelta)
    {
        return checked((long)Math.Round(
            timestampDelta * 1_000_000d / Stopwatch.Frequency,
            MidpointRounding.AwayFromZero));
    }

    private static void ApplyGcPressure()
    {
        var pressure = new byte[4 * 1024 * 1024];
        for (var index = 0; index < pressure.Length; index += 4096)
            pressure[index] = unchecked((byte)index);
        GC.KeepAlive(pressure);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private static void ValidateOptions(ScenarioRunOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseDirectory))
            throw new ArgumentException("Base directory must not be blank.", nameof(options));
        if (options.TransportPlanningWorkers <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.TransportPlanningWorkers));
        if (options.ForceGcEveryTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(options.ForceGcEveryTicks));
    }

    private sealed class ScenarioCounterAccumulator
    {
        private long _pathRequests;
        private long _transportIntake;
        private long _transportCompleted;
        private long _transportRequeued;
        private long _transportNoPath;
        private long _miningIntake;
        private long _craftIntake;
        private long _craftCompleted;
        private long _constructionIntake;
        private long _constructionSitesProcessed;

        internal void Add(RuntimeHeadlessMetricsSnapshot metrics)
        {
            _pathRequests += metrics.Paths.RequestsServedThisTick;
            _transportIntake += metrics.Planners.TransportIntakeThisTick;
            _transportCompleted += metrics.Planners.TransportCompletedThisTick;
            _transportRequeued += metrics.Planners.TransportRequeuedThisTick;
            _transportNoPath += metrics.Planners.TransportNoPathThisTick;
            _miningIntake += metrics.Planners.MiningIntakeThisTick;
            _craftIntake += metrics.Planners.CraftIntakeThisTick;
            _craftCompleted += metrics.Planners.CraftCompletedThisTick;
            _constructionIntake += metrics.Planners.ConstructionIntakeThisTick;
            _constructionSitesProcessed += metrics.Planners.ConstructionSitesProcessedThisTick;
        }

        internal ScenarioDeterministicCounters ToEvidence()
        {
            return new ScenarioDeterministicCounters(
                _pathRequests,
                _transportIntake,
                _transportCompleted,
                _transportRequeued,
                _transportNoPath,
                _miningIntake,
                _craftIntake,
                _craftCompleted,
                _constructionIntake,
                _constructionSitesProcessed);
        }
    }
}
