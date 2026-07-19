using System.Text.RegularExpressions;

internal static class DeterministicAuthoritySmokeTests
{
    private static readonly (string Name, Regex Pattern)[] ForbiddenAuthorityPatterns =
    {
        ("object GetHashCode()", new Regex(@"\bGetHashCode\s*\(", RegexOptions.Compiled)),
        ("dictionary Keys/Values view enumeration", new Regex(@"\.(Keys|Values)\b", RegexOptions.Compiled)),
        ("production Guid.NewGuid()", new Regex(@"\bGuid\.NewGuid\s*\(", RegexOptions.Compiled))
    };

    public static void RunAll()
    {
        Console.WriteLine("=== Deterministic Authority Smoke Tests ===");

        string root = TestRepositoryPaths.FindRepositoryRoot();
        TestSaveReplayAndHashAuthorityAvoidsUnstableInputs(root);
        TestNavigationPathfindingAvoidsWallClockBudgets(root);
        TestJobsOrchestratorAvoidsWallClockStats(root);
        TestSchedulerTuningsAvoidWallClockBudgets(root);
        TestRuntimeInvalidatesPathCachesForDirtyNavigationChunks(root);
        TestMovementExecutorUsesTunedStepPacing(root);
        TestTickSchedulerUsesDeterministicSystemOrder(root);
        TestSanitizeSystemUsesTickDerivedSchedule(root);
        TestCommandQueueUsesDeterministicOwnerQueue(root);
        TestEventBusUsesDeterministicHandlerLists(root);
        TestInfrastructureDictionariesUseDeterministicOwnerLocks(root);
        TestRuntimeAndTransportSequencesUseOwnerState(root);
        TestDiffTargetGuidEncodingIsPlatformStable(root);
        TestDiffLogUsesExplicitSystemOrder(root);
        TestLegacyEntityIdUseStaysCompatibilityScoped(root);
        TestMovementExecutorUsesWiderEntityKeys(root);
        TestRngAndCatalogSnapshotsUseStableOrdering(root);
        TestContentZoneDefinitionsUseStableOrdering(root);
        TestBiomeTemplateRegistryUsesStableOrdering(root);
        TestStockpileIndexesUseWiderItemKeys(root);
        TestItemManagerEntityKeyLookupIsIndexed(root);
        TestCreatureManagerEntityKeyLookupIsIndexed(root);
        TestZoneAndStockpileSnapshotsUseStableOrdering(root);
        TestTransportQueueShardSnapshotsUseStableOrdering(root);
        TestChunkLifecycleHeatDecayUsesStableOrdering(root);
        TestWorldAndPlaceableSnapshotsUseStableOrdering(root);
        TestWorldSavePayloadRestoreUsesCanonicalOrdering(root);
        TestConstructionMaterialSnapshotsUseStableOrdering(root);
        TestReservationManagerUsesWritePhaseDictionarySemantics(root);
        TestOrdersManagerUsesDeterministicOwnedCollections(root);
        TestOrderPlannerOutboxesUseDeterministicOwnerQueues(root);
        TestJobsBacklogsUseDeterministicOwnerQueues(root);
        TestTransportStatsAreExecutorOwned(root);
        TestProfessionSelectionUsesStableGuidTieBreaks(root);
        TestChunkTileReadsAreSynchronized(root);
        TestWorldGenerationSeedUsesExplicitEndianEncoding(root);

        Console.WriteLine("=== Deterministic Authority Smoke Tests Completed ===\n");
    }

    private static void TestSaveReplayAndHashAuthorityAvoidsUnstableInputs(string root)
    {
        var violations = new List<string>();
        foreach (var file in EnumerateAuthorityFiles(root))
        {
            string text = File.ReadAllText(file);
            string relative = TestRepositoryPaths.RelativePath(root, file);
            foreach (var rule in ForbiddenAuthorityPatterns)
            {
                if (rule.Pattern.IsMatch(text))
                    violations.Add($"{relative} contains {rule.Name}");
            }
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Deterministic save/replay/hash authority violations:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Save/replay/hash authority avoids unstable object hashes and unordered dictionary views");
    }

    private static void TestNavigationPathfindingAvoidsWallClockBudgets(string root)
    {
        var wallClockExecutionTokens = new[] { "Stopwatch", "ElapsedMilliseconds" };
        var executionFiles = new[]
        {
            Path.Combine(root, "src", "HumanFortress.Navigation", "DeterministicAStar.cs"),
            Path.Combine(root, "src", "HumanFortress.Navigation", "PathService.cs")
        };
        var wallClockBudgetTokens = new[] { "MaxMsPerTickPathing", "max_ms_per_tick_pathing" };
        var budgetFiles = new[]
        {
            Path.Combine(root, "src", "HumanFortress.Navigation", "NavigationTuning.cs"),
            Path.Combine(root, "content", "registries", "tuning.navigation.json")
        };

        var violations = new List<string>();
        foreach (var file in executionFiles)
        {
            string text = File.ReadAllText(file);
            foreach (string token in wallClockExecutionTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} contains {token}");
            }
        }

        foreach (var file in budgetFiles)
        {
            string text = File.ReadAllText(file);
            foreach (string token in wallClockBudgetTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} contains {token}");
            }
        }

        string pathServiceText = File.ReadAllText(executionFiles[1]);

        RegressionAssert.True(
            violations.Count == 0,
            "Navigation pathfinding must use deterministic node/request budgets, not wall-clock time:\n"
            + string.Join('\n', violations));
        RegressionAssert.True(
            !pathServiceText.Contains("ConcurrentQueue", StringComparison.Ordinal)
            && !pathServiceText.Contains("Queue<PathRequest>", StringComparison.Ordinal)
            && pathServiceText.Contains("NavPath.BudgetExhausted", StringComparison.Ordinal)
            && pathServiceText.Contains("CalculateNodeBudget", StringComparison.Ordinal)
            && pathServiceText.Contains("path.ReachesDestination(request.Destination)", StringComparison.Ordinal),
            "PathService must expose caller-owned deterministic retry and cache only terminal destination paths.");
        Console.WriteLine("[PASS] Navigation pathfinding avoids wall-clock budgets");
    }

    private static void TestJobsOrchestratorAvoidsWallClockStats(string root)
    {
        var forbiddenTokens = new[] { "Stopwatch", "ElapsedMilliseconds", "PlanMsTotal", "ApplyMsTotal" };
        var files = new[]
        {
            Path.Combine(root, "src", "HumanFortress.Jobs", "Orchestration", "UnifiedJobsOrchestrator.cs"),
            Path.Combine(root, "src", "HumanFortress.Contracts", "Runtime", "Snapshots", "SimulationJobsDebugData.cs"),
            Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "JobsDebugSnapshotBuilder.cs"),
            Path.Combine(root, "src", "HumanFortress.App", "UI", "UiWorkDrawerRenderer.Scheduler.cs")
        };

        var violations = new List<string>();
        foreach (var file in files)
        {
            string text = File.ReadAllText(file);
            foreach (string token in forbiddenTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                    violations.Add($"{TestRepositoryPaths.RelativePath(root, file)} contains {token}");
            }
        }

        string orchestratorText = File.ReadAllText(files[0]);
        string contractText = File.ReadAllText(files[1]);
        string uiText = File.ReadAllText(files[3]);

        RegressionAssert.True(
            violations.Count == 0
            && orchestratorText.Contains("PlanStageCount", StringComparison.Ordinal)
            && orchestratorText.Contains("ApplyStageCount", StringComparison.Ordinal)
            && contractText.Contains("int PlanStageCount", StringComparison.Ordinal)
            && contractText.Contains("int ApplyStageCount", StringComparison.Ordinal)
            && uiText.Contains("Stages P:", StringComparison.Ordinal),
            "Jobs scheduler debug stats must be deterministic stage/intake counters, not wall-clock milliseconds:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Jobs orchestrator avoids wall-clock scheduler stats");
    }

    private static void TestSchedulerTuningsAvoidWallClockBudgets(string root)
    {
        string tuningPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Configuration", "SchedulerTunings.cs");
        string contentPath = Path.Combine(root, "content", "registries", "tuning.scheduler.json");
        string tuningText = File.ReadAllText(tuningPath);
        string contentText = File.ReadAllText(contentPath);

        RegressionAssert.True(
            tuningText.Contains("record struct Budget(int PlanPerTick)", StringComparison.Ordinal)
            && !tuningText.Contains("int Ms", StringComparison.Ordinal)
            && !tuningText.Contains("\"ms\"", StringComparison.Ordinal)
            && !contentText.Contains("\"ms\"", StringComparison.Ordinal)
            && contentText.Contains("\"plan_per_tick\"", StringComparison.Ordinal),
            "Scheduler tuning budgets must remain deterministic item-count budgets, not wall-clock millisecond budgets.");
        Console.WriteLine("[PASS] Scheduler tunings avoid wall-clock budgets");
    }

    private static void TestRuntimeInvalidatesPathCachesForDirtyNavigationChunks(string root)
    {
        string postTickPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Host", "SimulationTickPipeline.PostTick.cs");
        string text = File.ReadAllText(postTickPath);

        RegressionAssert.True(
            text.Contains("RebuildChunkNavData", StringComparison.Ordinal)
            && text.Contains("InvalidateChunk", StringComparison.Ordinal),
            "Runtime post-tick navigation rebuild must invalidate active path caches for dirty chunks.");
        Console.WriteLine("[PASS] Runtime invalidates path caches when dirty navigation chunks rebuild");
    }

    private static void TestMovementExecutorUsesTunedStepPacing(string root)
    {
        string executorPath = Path.Combine(root, "src", "HumanFortress.Navigation", "MovementExecutor.cs");
        string tuningPath = Path.Combine(root, "src", "HumanFortress.Navigation", "NavigationTuning.cs");
        string contentPath = Path.Combine(root, "content", "registries", "tuning.navigation.json");
        string executorText = File.ReadAllText(executorPath);
        string tuningText = File.ReadAllText(tuningPath);
        string contentText = File.ReadAllText(contentPath);

        RegressionAssert.True(
            !executorText.Contains("_stepDelay = 2", StringComparison.Ordinal)
            && !executorText.Contains("visual movement", StringComparison.Ordinal)
            && executorText.Contains("_tuning.MovementStepDelayTicks", StringComparison.Ordinal)
            && tuningText.Contains("MovementStepDelayTicks", StringComparison.Ordinal)
            && tuningText.Contains("\"step_delay_ticks\"", StringComparison.Ordinal)
            && contentText.Contains("\"movement\"", StringComparison.Ordinal)
            && contentText.Contains("\"step_delay_ticks\"", StringComparison.Ordinal),
            "MovementExecutor pacing should come from deterministic navigation tuning, not a visual hardcoded delay.");
        Console.WriteLine("[PASS] Movement executor uses tuned deterministic step pacing");
    }

    private static void TestTickSchedulerUsesDeterministicSystemOrder(string root)
    {
        string schedulerPath = Path.Combine(root, "src", "HumanFortress.Core", "Time", "TickScheduler.cs");
        string hostLifecyclePath = Path.Combine(root, "src", "HumanFortress.Runtime", "Host", "SimulationRuntimeHost.Lifecycle.cs");
        string schedulerText = File.ReadAllText(schedulerPath);
        string hostLifecycleText = File.ReadAllText(hostLifecyclePath);

        RegressionAssert.True(
            schedulerText.Contains("_systems.Sort(CompareSystems)", StringComparison.Ordinal)
            && schedulerText.Contains("string.CompareOrdinal(a.SystemId, b.SystemId)", StringComparison.Ordinal)
            && schedulerText.Contains("string.IsNullOrWhiteSpace(systemId)", StringComparison.Ordinal)
            && schedulerText.Contains("Tick system id '{systemId}' is already registered.", StringComparison.Ordinal)
            && !schedulerText.Contains("Parallel.ForEach(systems", StringComparison.Ordinal)
            && schedulerText.Contains("MAX_CONSECUTIVE_SYSTEM_FAILURES", StringComparison.Ordinal)
            && schedulerText.Contains("readFailures.Contains(system)", StringComparison.Ordinal)
            && schedulerText.Contains("state.Quarantined = true", StringComparison.Ordinal)
            && !schedulerText.Contains("TODO: Implement quarantine logic", StringComparison.Ordinal)
            && hostLifecycleText.Contains("AttachForManualTicks", StringComparison.Ordinal)
            && hostLifecycleText.Contains("_core.Configure", StringComparison.Ordinal),
            "TickScheduler and Runtime manual tick host must use deterministic system order, avoid read-phase parallelism, and quarantine repeated system failures.");
        Console.WriteLine("[PASS] TickScheduler uses deterministic system ordering");
    }

    private static void TestSanitizeSystemUsesTickDerivedSchedule(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Jobs", "Safety", "SanitizeSystem.cs");
        string text = File.ReadAllText(path);

        RegressionAssert.True(
            !text.Contains("_counter", StringComparison.Ordinal)
            && text.Contains("tick + 1", StringComparison.Ordinal),
            "SanitizeSystem scheduling must be derived from the authoritative tick, not hidden runtime counters.");
        Console.WriteLine("[PASS] SanitizeSystem uses tick-derived scheduling");
    }

    private static void TestCommandQueueUsesDeterministicOwnerQueue(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Core", "Commands", "CommandQueue.cs");
        string text = File.ReadAllText(path);

        RegressionAssert.True(
            !text.Contains("ConcurrentQueue", StringComparison.Ordinal)
            && !text.Contains("System.Collections.Concurrent", StringComparison.Ordinal)
            && text.Contains("Queue<QueuedCommand> _pendingCommands", StringComparison.Ordinal)
            && text.Contains("private long _nextSequence", StringComparison.Ordinal)
            && text.Contains("commandsToExecute.Sort(CompareQueuedCommands)", StringComparison.Ordinal)
            && text.Contains("futureCommands.Sort", StringComparison.Ordinal)
            && text.Contains("_pendingCommands.Clear()", StringComparison.Ordinal)
            && text.Contains("CapturePendingCommandRecordsNoLock", StringComparison.Ordinal)
            && text.Contains(".ThenBy(queued => queued.Sequence)", StringComparison.Ordinal),
            "CommandQueue pending commands should remain lock-owned FIFO state with explicit sequence sorting, not ConcurrentQueue authority.");
        Console.WriteLine("[PASS] CommandQueue uses deterministic owner queue");
    }

    private static void TestEventBusUsesDeterministicHandlerLists(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Core", "Events", "EventBus.cs");
        string text = File.ReadAllText(path);

        RegressionAssert.True(
            !text.Contains("ConcurrentDictionary", StringComparison.Ordinal)
            && !text.Contains("AddOrUpdate", StringComparison.Ordinal)
            && !text.Contains("TryRemove", StringComparison.Ordinal)
            && !text.Contains("System.Collections.Concurrent", StringComparison.Ordinal)
            && text.Contains("Dictionary<Type, List<Delegate>> _handlers", StringComparison.Ordinal)
            && text.Contains("handlersCopy = new List<Delegate>(handlers)", StringComparison.Ordinal)
            && text.Contains("handlers.Add(handler)", StringComparison.Ordinal)
            && text.Contains("_handlers.Remove(eventType)", StringComparison.Ordinal),
            "EventBus should keep deterministic lock-owned handler lists instead of concurrent dictionary mutation callbacks.");
        Console.WriteLine("[PASS] EventBus uses deterministic handler lists");
    }

    private static void TestInfrastructureDictionariesUseDeterministicOwnerLocks(string root)
    {
        string rngPath = Path.Combine(root, "src", "HumanFortress.Core", "Random", "RngStreamManager.cs");
        string worldPath = Path.Combine(root, "src", "HumanFortress.Simulation", "World", "World.cs");
        string navigationManagerPath = Path.Combine(root, "src", "HumanFortress.Navigation", "NavigationManager.cs");
        string pathCachePath = Path.Combine(root, "src", "HumanFortress.Navigation", "PathCache.cs");

        var files = new[]
        {
            rngPath,
            worldPath,
            navigationManagerPath,
            pathCachePath
        };

        var textByFile = files.ToDictionary(
            file => TestRepositoryPaths.RelativePath(root, file),
            File.ReadAllText,
            StringComparer.Ordinal);
        string combined = string.Join('\n', textByFile.Values);
        string rngText = textByFile[TestRepositoryPaths.RelativePath(root, rngPath)];
        string worldText = textByFile[TestRepositoryPaths.RelativePath(root, worldPath)];
        string navigationManagerText = textByFile[TestRepositoryPaths.RelativePath(root, navigationManagerPath)];
        string pathCacheText = textByFile[TestRepositoryPaths.RelativePath(root, pathCachePath)];

        RegressionAssert.True(
            !combined.Contains("ConcurrentDictionary", StringComparison.Ordinal)
            && !combined.Contains("System.Collections.Concurrent", StringComparison.Ordinal)
            && !combined.Contains("AddOrUpdate", StringComparison.Ordinal)
            && !combined.Contains("TryRemove", StringComparison.Ordinal)
            && !combined.Contains("GetOrAdd", StringComparison.Ordinal)
            && !combined.Contains("Interlocked.", StringComparison.Ordinal)
            && rngText.Contains("Dictionary<string, DeterministicRng> _streams", StringComparison.Ordinal)
            && rngText.Contains("private readonly object _sync", StringComparison.Ordinal)
            && rngText.Contains("GetOrderedStreams()", StringComparison.Ordinal)
            && rngText.Contains("OrderBy(static kvp => kvp.Key, StringComparer.Ordinal)", StringComparison.Ordinal)
            && worldText.Contains("Dictionary<ChunkKey, Chunk> _chunks", StringComparison.Ordinal)
            && worldText.Contains("private readonly object _chunkLock", StringComparison.Ordinal)
            && worldText.Contains("return OrderChunks(_chunks.Values).ToArray();", StringComparison.Ordinal)
            && worldText.Contains("return OrderChunks(_chunks.Values.Where", StringComparison.Ordinal)
            && navigationManagerText.Contains("Dictionary<ChunkKey, ChunkNavData> _navData", StringComparison.Ordinal)
            && navigationManagerText.Contains("private readonly object _sync", StringComparison.Ordinal)
            && navigationManagerText.Contains("_navData.Add(key, navData)", StringComparison.Ordinal)
            && pathCacheText.Contains("Dictionary<ulong, CacheEntry> _cache", StringComparison.Ordinal)
            && pathCacheText.Contains("Dictionary<ChunkKey, HashSet<ulong>> _chunkIndex", StringComparison.Ordinal)
            && pathCacheText.Contains("foreach (var kvp in _cache.OrderBy(static kvp => kvp.Key))", StringComparison.Ordinal)
            && pathCacheText.Contains("RemoveFromIndex(key)", StringComparison.Ordinal),
            "Infrastructure dictionaries should remain owner-lock state with stable snapshot/cache ordering, not concurrent callback state.");
        Console.WriteLine("[PASS] Infrastructure dictionaries use deterministic owner locks");
    }

    private static void TestRuntimeAndTransportSequencesUseOwnerState(string root)
    {
        string runtimeServicesPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Session", "RuntimeSessionServices.cs");
        string transportQueuePath = Path.Combine(root, "src", "HumanFortress.Simulation", "Jobs", "TransportRequestQueue.cs");
        string runtimeServicesText = File.ReadAllText(runtimeServicesPath);
        string transportQueueText = File.ReadAllText(transportQueuePath);
        string combined = runtimeServicesText + '\n' + transportQueueText;

        RegressionAssert.True(
            !combined.Contains("Interlocked", StringComparison.Ordinal)
            && !combined.Contains("using System.Threading", StringComparison.Ordinal)
            && runtimeServicesText.Contains("private readonly object _identitySequenceLock", StringComparison.Ordinal)
            && runtimeServicesText.Contains("_nextCommandIdentitySequence++", StringComparison.Ordinal)
            && runtimeServicesText.Contains("_nextCommandIdentitySequence < sequence", StringComparison.Ordinal)
            && transportQueueText.Contains("_enqueuedTotal++", StringComparison.Ordinal)
            && transportQueueText.Contains("lock (_lock)", StringComparison.Ordinal),
            "Runtime command identity and transport queue counters should stay owner-state, not atomic primitives in authority paths.");
        Console.WriteLine("[PASS] Runtime and transport sequences use owner state");
    }

    private static void TestDiffTargetGuidEncodingIsPlatformStable(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Core", "Simulation", "DiffTargetEncoding.cs");
        string worldCellPath = Path.Combine(root, "src", "HumanFortress.Simulation", "World", "WorldCellTargetEncoding.cs");
        string guidGeneratorPath = Path.Combine(root, "src", "HumanFortress.Core", "Random", "DeterministicGuidGenerator.cs");
        string text = File.ReadAllText(path);
        string worldCellText = File.ReadAllText(worldCellPath);
        string guidGeneratorText = File.ReadAllText(guidGeneratorPath);

        RegressionAssert.True(
            !text.Contains("BitConverter.ToUInt32", StringComparison.Ordinal)
            && text.Contains("BinaryPrimitives.ReadUInt32LittleEndian", StringComparison.Ordinal)
            && text.Contains("BinaryPrimitives.ReadUInt64LittleEndian", StringComparison.Ordinal)
            && !guidGeneratorText.Contains("BitConverter.GetBytes", StringComparison.Ordinal)
            && guidGeneratorText.Contains("BinaryPrimitives.WriteUInt32LittleEndian", StringComparison.Ordinal)
            && !guidGeneratorText.Contains("GenerateFromPosition(ulong tickSeed, int x, int y, int z)", StringComparison.Ordinal)
            && guidGeneratorText.Contains("GenerateFromPosition(ulong tickSeed, int x, int y, int z, ulong scopeSalt)", StringComparison.Ordinal)
            && guidGeneratorText.Contains("HashUInt64(positionHash, scopeSalt)", StringComparison.Ordinal)
            && text.Contains("ForWorldCell(int worldX, int worldY, int z, Guid entityGuid)", StringComparison.Ordinal)
            && text.Contains("ForChunkLocal(int chunkX, int chunkY, int z, int localX, int localY, Guid entityGuid)", StringComparison.Ordinal)
            && text.Contains("ForEncodedTarget(int chunkId, int localIndex, Guid entityGuid)", StringComparison.Ordinal)
            && worldCellText.Contains("ToDiffTarget(Guid entityGuid)", StringComparison.Ordinal)
            && worldCellText.Contains("DiffTargetEncoding.ForEncodedTarget", StringComparison.Ordinal)
            && !worldCellText.Contains("ToDiffTarget(int entityId, ulong entityKey)", StringComparison.Ordinal),
            "DiffTargetEncoding.Guid projection must use explicit endian order, wider entity keys, and GUID target helpers for replay portability.");
        Console.WriteLine("[PASS] Diff target GUID projection uses explicit endian entity encoding");
    }

    private static void TestDiffLogUsesExplicitSystemOrder(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Core", "Simulation", "DiffLog.cs");
        string text = File.ReadAllText(path);
        int effectiveEntityKeyCompareIndex = text.IndexOf("a.Target.EffectiveEntityKey.CompareTo", StringComparison.Ordinal);
        int legacyEntityIdCompareIndex = text.IndexOf("a.Target.EntityId.CompareTo", StringComparison.Ordinal);

        RegressionAssert.True(
            text.Contains("SystemOrder", StringComparison.Ordinal)
            && text.Contains("LocalSeq", StringComparison.Ordinal)
            && text.Contains("EntityKey", StringComparison.Ordinal)
            && text.Contains("EffectiveEntityKey", StringComparison.Ordinal)
            && effectiveEntityKeyCompareIndex >= 0
            && legacyEntityIdCompareIndex > effectiveEntityKeyCompareIndex
            && !text.Contains("SystemPrecedence", StringComparison.Ordinal)
            && !text.Contains("StableSystemHash8", StringComparison.Ordinal),
            "DiffLog must use explicit numeric system order, local sequence, and wider entity keys before legacy entity-id compatibility tie-breaks.");
        Console.WriteLine("[PASS] DiffLog uses explicit system order, local sequence hooks, and wider entity keys");
    }

    private static void TestLegacyEntityIdUseStaysCompatibilityScoped(string root)
    {
        string sourceRoot = Path.Combine(root, "src");
        var violations = new List<string>();
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(sourceRoot))
        {
            string relative = TestRepositoryPaths.RelativePath(root, file);
            if (relative == "src/HumanFortress.Core/Simulation/DiffTargetEncoding.cs")
                continue;

            string text = File.ReadAllText(file);
            if (text.Contains("SignedEntityId(", StringComparison.Ordinal))
                violations.Add($"{relative} calls SignedEntityId");

            if (text.Contains("new DiffTarget(", StringComparison.Ordinal))
                violations.Add($"{relative} constructs DiffTarget directly");
        }

        RegressionAssert.True(
            violations.Count == 0,
            "Production DiffTarget legacy projection/construction must stay scoped to the Core encoding compatibility helper:\n" + string.Join('\n', violations));
        Console.WriteLine("[PASS] Legacy diff target projection stays compatibility-scoped");
    }

    private static void TestMovementExecutorUsesWiderEntityKeys(string root)
    {
        string contractPath = Path.Combine(root, "src", "HumanFortress.Contracts", "Navigation", "MovementContracts.cs");
        string implementationPath = Path.Combine(root, "src", "HumanFortress.Navigation", "MovementExecutor.cs");
        string runtimeNavigationServicesPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Navigation", "RuntimeNavigationServices.cs");
        string miningJobSystemPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Jobs", "MiningJobSystem.cs");
        string transportJobSystemPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Jobs", "TransportJobSystem.cs");
        string craftJobSystemPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Jobs", "CraftJobSystem.cs");
        string runtimeHostPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Host", "SimulationRuntimeHost.cs");
        string runtimeHostAccessorsPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Host", "SimulationRuntimeHost.Accessors.cs");
        string sessionSnapshotAccessPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "FortressRuntimeSessionSnapshotFacade.SessionAccess.cs");
        string sessionSnapshotMapPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "FortressRuntimeSessionSnapshotFacade.Map.cs");
        string navigationOverlayPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "NavigationOverlaySnapshotBuilder.cs");
        string contractText = File.ReadAllText(contractPath);
        string implementationText = File.ReadAllText(implementationPath);
        string runtimeNavigationServicesText = File.ReadAllText(runtimeNavigationServicesPath);
        string miningJobSystemText = File.ReadAllText(miningJobSystemPath);
        string transportJobSystemText = File.ReadAllText(transportJobSystemPath);
        string craftJobSystemText = File.ReadAllText(craftJobSystemPath);
        string runtimeHostText = File.ReadAllText(runtimeHostPath);
        string runtimeHostAccessorsText = File.ReadAllText(runtimeHostAccessorsPath);
        string sessionSnapshotAccessText = File.ReadAllText(sessionSnapshotAccessPath);
        string sessionSnapshotMapText = File.ReadAllText(sessionSnapshotMapPath);
        string navigationOverlayText = File.ReadAllText(navigationOverlayPath);

        RegressionAssert.True(
            contractText.Contains("ulong entityKey", StringComparison.Ordinal)
            && !contractText.Contains("uint entityId", StringComparison.Ordinal)
            && implementationText.Contains("Dictionary<ulong, MovementState>", StringComparison.Ordinal)
            && runtimeNavigationServicesText.Contains("new PathService(_tuning)", StringComparison.Ordinal)
            && runtimeNavigationServicesText.Contains("_pathServices?.Register(paths)", StringComparison.Ordinal)
            && runtimeNavigationServicesText.Contains("new WorldNavigationView(navigation)", StringComparison.Ordinal)
            && runtimeNavigationServicesText.Contains("CreatePathQueryServices", StringComparison.Ordinal)
            && runtimeNavigationServicesText.Contains("new MovementExecutor(query.PathService, _tuning)", StringComparison.Ordinal)
            && runtimeHostText.Contains("private readonly RuntimePathServiceRegistry? _pathServices", StringComparison.Ordinal)
            && runtimeHostText.Contains("_pathServices = pathServices", StringComparison.Ordinal)
            && runtimeHostAccessorsText.Contains("internal RuntimePathServiceRegistry? PathServices => _pathServices", StringComparison.Ordinal)
            && sessionSnapshotAccessText.Contains("new RuntimeNavigationServices(host.PathServices, host.NavigationTuning)", StringComparison.Ordinal)
            && sessionSnapshotMapText.Contains("NavigationServices(session)", StringComparison.Ordinal)
            && navigationOverlayText.Contains("navigationServices ?? new RuntimeNavigationServices(null, activeTuning)", StringComparison.Ordinal)
            && miningJobSystemText.Contains("RuntimeNavigationServices?", StringComparison.Ordinal)
            && transportJobSystemText.Contains("RuntimeNavigationServices?", StringComparison.Ordinal)
            && craftJobSystemText.Contains("RuntimeNavigationServices?", StringComparison.Ordinal)
            && !miningJobSystemText.Contains("new PathService", StringComparison.Ordinal)
            && !miningJobSystemText.Contains("new MovementExecutor", StringComparison.Ordinal)
            && !transportJobSystemText.Contains("new PathService", StringComparison.Ordinal)
            && !transportJobSystemText.Contains("new MovementExecutor", StringComparison.Ordinal)
            && !craftJobSystemText.Contains("new PathService", StringComparison.Ordinal)
            && !craftJobSystemText.Contains("new MovementExecutor", StringComparison.Ordinal),
            "Movement execution state must use wider entity keys, and Runtime job wrappers must obtain path/movement services through RuntimeNavigationServices.");
        Console.WriteLine("[PASS] Movement execution uses wider entity keys");
    }

    private static void TestStockpileIndexesUseWiderItemKeys(string root)
    {
        string diffPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Stockpile", "StockpileDiff.cs");
        string applicatorPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Stockpile", "StockpileDiffApplicator.cs");
        string dataPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Stockpile", "ChunkStockpileData.cs");
        string transportEmitterPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Diff", "TransportStockpileIndexEmitter.cs");
        string diffText = File.ReadAllText(diffPath);
        string applicatorText = File.ReadAllText(applicatorPath);
        string dataText = File.ReadAllText(dataPath);
        string transportEmitterText = File.ReadAllText(transportEmitterPath);

        RegressionAssert.True(
            diffText.Contains("ulong ItemHandle", StringComparison.Ordinal)
            && applicatorText.Contains("GetInstanceByEntityKey", StringComparison.Ordinal)
            && dataText.Contains("Dictionary<string, List<ulong>> _itemsByTag", StringComparison.Ordinal)
            && dataText.Contains("Dictionary<int, List<ulong>> _itemsByZone", StringComparison.Ordinal)
            && dataText.Contains("List<ulong> _looseItems", StringComparison.Ordinal)
            && transportEmitterText.Contains("DiffTargetEncoding.EntityKey(itemId)", StringComparison.Ordinal)
            && !transportEmitterText.Contains("SignedEntityId(itemId)", StringComparison.Ordinal),
            "Stockpile item indexes and diffs must use wider entity keys, not legacy truncated item ids.");
        Console.WriteLine("[PASS] Stockpile item indexes use wider entity keys");
    }

    private static void TestContentZoneDefinitionsUseStableOrdering(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Content", "Loading", "FortressRuntimeContentSnapshot.cs");
        string text = File.ReadAllText(path);

        RegressionAssert.True(
            text.Contains("ZoneDefinitions = ZonesById", StringComparison.Ordinal)
            && text.Contains("OrderBy(zone => zone.Key, StringComparer.Ordinal)", StringComparison.Ordinal)
            && !text.Contains("ZoneDefinitions = ZonesById.Values.ToArray();", StringComparison.Ordinal),
            "Content runtime zone definition snapshots must be stable by zone id before Runtime applies them to Simulation.");
        Console.WriteLine("[PASS] Content zone definitions use stable ordering");
    }

    private static void TestRngAndCatalogSnapshotsUseStableOrdering(string root)
    {
        string deterministicRngPath = Path.Combine(root, "src", "HumanFortress.Core", "Random", "DeterministicRng.cs");
        string rngPath = Path.Combine(root, "src", "HumanFortress.Core", "Random", "RngStreamManager.cs");
        string materialPath = Path.Combine(root, "src", "HumanFortress.Content", "Registry", "MaterialRegistry.cs");
        string itemCatalogPath = Path.Combine(root, "src", "HumanFortress.Contracts", "Simulation", "Items", "ItemDefinitionCatalogStore.cs");
        string creatureCatalogPath = Path.Combine(root, "src", "HumanFortress.Contracts", "Simulation", "Creatures", "CreatureDefinitionCatalogStore.cs");
        string constructionCatalogPath = Path.Combine(root, "src", "HumanFortress.Contracts", "Content", "Registry", "ConstructionCatalogStore.cs");
        string recipeCatalogPath = Path.Combine(root, "src", "HumanFortress.Contracts", "Content", "Registry", "RecipeDefinition.cs");
        string constructionLoaderPath = Path.Combine(root, "src", "HumanFortress.Content", "Definitions", "CoreDataRegistryLoader.Constructions.cs");
        string runtimeSaveSnapshotPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotData.cs");
        string deterministicRngText = File.ReadAllText(deterministicRngPath);
        string rngText = File.ReadAllText(rngPath);
        string materialText = File.ReadAllText(materialPath);
        string itemCatalogText = File.ReadAllText(itemCatalogPath);
        string creatureCatalogText = File.ReadAllText(creatureCatalogPath);
        string constructionCatalogText = File.ReadAllText(constructionCatalogPath);
        string recipeCatalogText = File.ReadAllText(recipeCatalogPath);
        string constructionLoaderText = File.ReadAllText(constructionLoaderPath);
        string runtimeSaveSnapshotText = File.ReadAllText(runtimeSaveSnapshotPath);

        RegressionAssert.True(
            deterministicRngText.Contains("NextSplitMix64", StringComparison.Ordinal)
            && deterministicRngText.Contains("0xBF58476D1CE4E5B9UL", StringComparison.Ordinal)
            && rngText.Contains("FnvOffsetBasis", StringComparison.Ordinal)
            && rngText.Contains("DeterministicRng.MixSeed(_masterSeed ^ hash)", StringComparison.Ordinal)
            && !rngText.Contains("streamSeed = streamSeed * 31 + c", StringComparison.Ordinal)
            && rngText.Contains("GetOrderedStreams()", StringComparison.Ordinal)
            && rngText.Contains("OrderBy(static kvp => kvp.Key, StringComparer.Ordinal)", StringComparison.Ordinal)
            && !rngText.Contains("ConcurrentDictionary", StringComparison.Ordinal)
            && materialText.Contains("OrderBy(static material => material.StringId, StringComparer.Ordinal)", StringComparison.Ordinal)
            && materialText.Contains("OrderBy(static entry => entry.Key, StringComparer.Ordinal)", StringComparison.Ordinal)
            && itemCatalogText.Contains("OrderBy(static definition => definition.Id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && itemCatalogText.Contains("OrderBy(static id => id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && creatureCatalogText.Contains("OrderBy(static definition => definition.Id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && constructionCatalogText.Contains("OrderBy(static construction => construction.Id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && constructionCatalogText.Contains("OrderBy(static category => category, StringComparer.Ordinal)", StringComparison.Ordinal)
            && recipeCatalogText.Contains("OrderBy(static definition => definition.Id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && recipeCatalogText.Contains("string.Compare(left.Id, right.Id, StringComparison.Ordinal)", StringComparison.Ordinal)
            && constructionLoaderText.Contains("OrderBy(static definition => definition.Id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && runtimeSaveSnapshotText.Contains("OrderBy(static stream => stream.StreamName, StringComparer.Ordinal)", StringComparison.Ordinal)
            && runtimeSaveSnapshotText.Contains("CommandReplayJournalHashBuilder.Build(records)", StringComparison.Ordinal)
            && runtimeSaveSnapshotText.Contains("CommandReplayJournalHashBuilder.Build(pendingRecords)", StringComparison.Ordinal)
            && !itemCatalogText.Contains("return _definitions.Values;", StringComparison.Ordinal)
            && !creatureCatalogText.Contains("return _definitions.Values;", StringComparison.Ordinal)
            && !constructionCatalogText.Contains("return _constructionsById.Values;", StringComparison.Ordinal)
            && !recipeCatalogText.Contains("return _recipes.Values;", StringComparison.Ordinal),
            "RNG and catalog snapshot APIs must expose stable key/content-id ordering.");
        Console.WriteLine("[PASS] RNG and catalog snapshots use stable ordering");
    }

    private static void TestBiomeTemplateRegistryUsesStableOrdering(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Content", "Registry", "BiomeTemplateRegistry.cs");
        string text = File.ReadAllText(path);

        RegressionAssert.True(
            CountOccurrences(text, "ThenBy(static template => template.Id, StringComparer.Ordinal)") >= 2
            && text.Contains("OrderBy(static id => id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && text.Contains("GetAllTemplates() => _allTemplates", StringComparison.Ordinal)
            && !text.Contains("_templatesById.Keys.ToList()", StringComparison.Ordinal),
            "Biome template registry ids and broad template snapshots must use stable content-id ordering.");
        Console.WriteLine("[PASS] Biome template registry uses stable ordering");
    }

    private static void TestItemManagerEntityKeyLookupIsIndexed(string root)
    {
        string managerPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Items", "ItemManager.cs");
        string indexingPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Items", "ItemManager.Indexing.cs");
        string queriesPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Items", "ItemManager.Queries.cs");
        string mutationsPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Items", "ItemManager.Mutations.cs");
        string spawningPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Items", "ItemManager.Spawning.cs");
        string stackPolicyPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Items", "ItemStackPolicy.cs");
        string restorePath = Path.Combine(root, "src", "HumanFortress.Simulation", "Items", "ItemManager.SaveRestore.cs");
        string applicatorPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Diff", "SimulationDiffApplicator.Targets.cs");
        string managerText = File.ReadAllText(managerPath);
        string indexingText = File.ReadAllText(indexingPath);
        string queriesText = File.ReadAllText(queriesPath);
        string mutationsText = File.ReadAllText(mutationsPath);
        string spawningText = File.ReadAllText(spawningPath);
        string stackPolicyText = File.ReadAllText(stackPolicyPath);
        string restoreText = File.ReadAllText(restorePath);
        string applicatorText = File.ReadAllText(applicatorPath);
        string entityKeyLookupBody = ExtractBetween(
            queriesText,
            "internal ItemInstance? GetInstanceByEntityKey",
            "/// <summary>\n    /// Get all instances");
        string legacyLookupBody = ExtractBetween(
            queriesText,
            "internal ItemInstance? GetInstanceByEntityId",
            "/// <summary>\n    /// Find an item by the wider stable entity key");
        string findItemBody = ExtractBetween(
            applicatorText,
            "private static ItemInstance? FindItemByTarget",
            "private static CreatureInstance? FindCreatureByTarget");

        RegressionAssert.True(
            managerText.Contains("LiveEntityIdentityIndex _identityIndex", StringComparison.Ordinal)
            && managerText.Contains("Dictionary<uint, List<Guid>> _legacyEntityIdIndex", StringComparison.Ordinal)
            && indexingText.Contains("EntityKeyIndexAdd", StringComparison.Ordinal)
            && indexingText.Contains("_identityIndex.TryAdd", StringComparison.Ordinal)
            && indexingText.Contains("EntityKeyIndexRemove", StringComparison.Ordinal)
            && indexingText.Contains("LegacyEntityIdIndexAdd", StringComparison.Ordinal)
            && indexingText.Contains("list.Sort();", StringComparison.Ordinal)
            && queriesText.Contains("_identityIndex.TryResolve", StringComparison.Ordinal)
            && queriesText.Contains("_legacyEntityIdIndex.TryGetValue", StringComparison.Ordinal)
            && (queriesText.Contains("OrderBy(static inst => inst.Guid)", StringComparison.Ordinal)
                || queriesText.Contains("OrderBy(static entry => entry.Key)", StringComparison.Ordinal))
            && queriesText.Contains("OrderItemsSpatially", StringComparison.Ordinal)
            && queriesText.Contains("ThenBy(static item => item.Guid)", StringComparison.Ordinal)
            && mutationsText.Contains(".OrderBy(static id => id)", StringComparison.Ordinal)
            && mutationsText.Contains("RemoveInstanceLocked(sourceId, source", StringComparison.Ordinal)
            && !mutationsText.Contains("_posIndex[key] = new List<Guid>", StringComparison.Ordinal)
            && stackPolicyText.Contains("TryGetCapacity", StringComparison.Ordinal)
            && stackPolicyText.Contains("AreCompatible", StringComparison.Ordinal)
            && stackPolicyText.Contains("ReservationTokens.Count != 0", StringComparison.Ordinal)
            && spawningText.Contains("ItemStackPolicy.TryGetCapacity", StringComparison.Ordinal)
            && spawningText.Contains("GetGroundItemsAtLocked", StringComparison.Ordinal)
            && spawningText.Contains("OrderBy(static item => item.Guid)", StringComparison.Ordinal)
            && !spawningText.Contains("_instances.Values", StringComparison.Ordinal)
            && restoreText.Contains("_identityIndex.TryReplace", StringComparison.Ordinal)
            && restoreText.Contains("_legacyEntityIdIndex.Clear()", StringComparison.Ordinal)
            && restoreText.Contains("LegacyEntityIdIndexAdd(instance.Guid)", StringComparison.Ordinal)
            && !entityKeyLookupBody.Contains("foreach", StringComparison.Ordinal)
            && !legacyLookupBody.Contains("foreach", StringComparison.Ordinal)
            && findItemBody.Contains("GetInstanceByEntityKey(target.EntityKey)", StringComparison.Ordinal)
            && findItemBody.Contains("TryLegacyEntityId(target.EntityId", StringComparison.Ordinal)
            && !findItemBody.Contains("(uint)target.EntityId", StringComparison.Ordinal),
            "ItemManager lookup and stack mutation must remain indexed, deterministic, compatibility-aware, and maintained across restore.");
        Console.WriteLine("[PASS] ItemManager entity lookup is indexed");
    }

    private static void TestCreatureManagerEntityKeyLookupIsIndexed(string root)
    {
        string managerPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Creatures", "CreatureManager.cs");
        string indexingPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Creatures", "CreatureManager.Indexing.cs");
        string queriesPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Creatures", "CreatureManager.Queries.cs");
        string spawningPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Creatures", "CreatureManager.Spawning.cs");
        string restorePath = Path.Combine(root, "src", "HumanFortress.Simulation", "Creatures", "CreatureManager.SaveRestore.cs");
        string applicatorPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Diff", "SimulationDiffApplicator.Targets.cs");
        string managerText = File.ReadAllText(managerPath);
        string indexingText = File.ReadAllText(indexingPath);
        string queriesText = File.ReadAllText(queriesPath);
        string spawningText = File.ReadAllText(spawningPath);
        string restoreText = File.ReadAllText(restorePath);
        string applicatorText = File.ReadAllText(applicatorPath);
        string entityKeyLookupBody = ExtractBetween(
            queriesText,
            "internal CreatureInstance? GetInstanceByEntityKey",
            "/// <summary>\n    /// Get all instances");
        string legacyLookupBody = ExtractBetween(
            queriesText,
            "internal CreatureInstance? GetInstanceByEntityId",
            "/// <summary>\n    /// Find a creature by the wider stable entity key");
        string findCreatureByTargetBody = ExtractBetween(
            applicatorText,
            "private static CreatureInstance? FindCreatureByTarget",
            "private static CreatureInstance? FindCreatureByEntityArgument");
        string findCreatureByArgumentBody = ExtractBetween(
            applicatorText,
            "private static CreatureInstance? FindCreatureByEntityArgument",
            "private static bool TryLegacyEntityId");

        RegressionAssert.True(
            managerText.Contains("LiveEntityIdentityIndex _identityIndex", StringComparison.Ordinal)
            && managerText.Contains("Dictionary<uint, List<Guid>> _legacyEntityIdIndex", StringComparison.Ordinal)
            && indexingText.Contains("EntityKeyIndexAdd", StringComparison.Ordinal)
            && indexingText.Contains("_identityIndex.TryAdd", StringComparison.Ordinal)
            && indexingText.Contains("LegacyEntityIdIndexAdd", StringComparison.Ordinal)
            && queriesText.Contains("_identityIndex.TryResolve", StringComparison.Ordinal)
            && queriesText.Contains("_legacyEntityIdIndex.TryGetValue", StringComparison.Ordinal)
            && (queriesText.Contains("OrderBy(static inst => inst.Guid)", StringComparison.Ordinal)
                || queriesText.Contains("OrderBy(static entry => entry.Key)", StringComparison.Ordinal))
            && spawningText.Contains("CalculateMaxHitPoints(def)", StringComparison.Ordinal)
            && spawningText.Contains("definition.BaseToughness * 5", StringComparison.Ordinal)
            && !spawningText.Contains("var maxHP = 100", StringComparison.Ordinal)
            && restoreText.Contains("_identityIndex.TryReplace", StringComparison.Ordinal)
            && restoreText.Contains("_legacyEntityIdIndex.Clear()", StringComparison.Ordinal)
            && restoreText.Contains("LegacyEntityIdIndexAdd(instance.Guid)", StringComparison.Ordinal)
            && !entityKeyLookupBody.Contains("foreach", StringComparison.Ordinal)
            && !legacyLookupBody.Contains("foreach", StringComparison.Ordinal)
            && findCreatureByTargetBody.Contains("GetInstanceByEntityKey(target.EntityKey)", StringComparison.Ordinal)
            && findCreatureByTargetBody.Contains("TryLegacyEntityId(target.EntityId", StringComparison.Ordinal)
            && findCreatureByArgumentBody.Contains("GetInstanceByEntityKey(op.Args)", StringComparison.Ordinal)
            && findCreatureByArgumentBody.Contains("TryLegacyEntityId(op.Args", StringComparison.Ordinal)
            && !applicatorText.Contains("GetAllInstances().FirstOrDefault", StringComparison.Ordinal)
            && !applicatorText.Contains("(uint)op.Target.EntityId", StringComparison.Ordinal)
            && !applicatorText.Contains("(uint)op.Args", StringComparison.Ordinal),
            "Creature entity lookup must stay indexed and SimulationDiffApplicator must prefer entity keys while guarding legacy fallback.");
        Console.WriteLine("[PASS] CreatureManager entity lookup is indexed");
    }

    private static void TestZoneAndStockpileSnapshotsUseStableOrdering(string root)
    {
        string zoneManagerPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Zones", "ZoneManager.cs");
        string zoneInstancePath = Path.Combine(root, "src", "HumanFortress.Simulation", "Zones", "ZoneInstance.cs");
        string chunkZoneDataPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Zones", "ChunkZoneData.cs");
        string zoneCoordinatorPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Zones", "ZoneCoordinator.cs");
        string stockpileManagerPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Stockpile", "StockpileManager.cs");
        string stockpileZonePath = Path.Combine(root, "src", "HumanFortress.Simulation", "Stockpile", "StockpileZone.cs");
        string chunkStockpileDataPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Stockpile", "ChunkStockpileData.cs");
        string stockpileWorldQueriesPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Stockpile", "StockpileWorldQueries.cs");
        string stockpilePayloadBuilderPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadBuilder.Stockpiles.cs");
        string replayHashPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Replay", "WorldReplayHashBuilder.Stockpiles.cs");
        string stockpileOverlayPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "StockpileSnapshotBuilder.Overlay.cs");
        string stockpileDetailPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "StockpileSnapshotBuilder.Detail.cs");
        string zoneDetailPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "ZoneOverlaySnapshotBuilder.Detail.cs");
        string zoneManagerText = File.ReadAllText(zoneManagerPath);
        string zoneInstanceText = File.ReadAllText(zoneInstancePath);
        string chunkZoneDataText = File.ReadAllText(chunkZoneDataPath);
        string zoneCoordinatorText = File.ReadAllText(zoneCoordinatorPath);
        string stockpileManagerText = File.ReadAllText(stockpileManagerPath);
        string stockpileZoneText = File.ReadAllText(stockpileZonePath);
        string chunkStockpileDataText = File.ReadAllText(chunkStockpileDataPath);
        string stockpileWorldQueriesText = File.ReadAllText(stockpileWorldQueriesPath);
        string stockpilePayloadBuilderText = File.ReadAllText(stockpilePayloadBuilderPath);
        string replayHashText = File.ReadAllText(replayHashPath);
        string stockpileOverlayText = File.ReadAllText(stockpileOverlayPath);
        string stockpileDetailText = File.ReadAllText(stockpileDetailPath);
        string zoneDetailText = File.ReadAllText(zoneDetailPath);

        RegressionAssert.True(
            (zoneManagerText.Contains("OrderBy(static definition => definition.Id, StringComparer.Ordinal)", StringComparison.Ordinal)
                || zoneManagerText.Contains("OrderBy(static entry => entry.Key, StringComparer.Ordinal)", StringComparison.Ordinal))
            && CountOccurrences(zoneManagerText, "OrderBy(static zone => zone.ZoneId)")
                + CountOccurrences(zoneManagerText, "OrderBy(static entry => entry.Key)") >= 2
            && zoneInstanceText.Contains("GetMemberChunksSnapshot", StringComparison.Ordinal)
            && (chunkZoneDataText.Contains("OrderBy(static shard => shard.ZoneId)", StringComparison.Ordinal)
                || chunkZoneDataText.Contains("OrderBy(static entry => entry.Key)", StringComparison.Ordinal))
            && zoneCoordinatorText.Contains("zone.GetMemberChunksSnapshot()", StringComparison.Ordinal)
            && (stockpileManagerText.Contains("OrderBy(static zone => zone.ZoneId)", StringComparison.Ordinal)
                || stockpileManagerText.Contains("OrderBy(static entry => entry.Key)", StringComparison.Ordinal))
            && stockpileZoneText.Contains("GetMemberChunksSnapshot", StringComparison.Ordinal)
            && (chunkStockpileDataText.Contains("OrderBy(static shard => shard.ZoneId)", StringComparison.Ordinal)
                || chunkStockpileDataText.Contains("OrderBy(static entry => entry.Key)", StringComparison.Ordinal))
            && CountOccurrences(chunkStockpileDataText, "OrderBy(static handle => handle)") >= 3
            && stockpileWorldQueriesText.Contains("zone.GetMemberChunksSnapshot()", StringComparison.Ordinal)
            && stockpilePayloadBuilderText.Contains("zone.GetMemberChunksSnapshot()", StringComparison.Ordinal)
            && replayHashText.Contains("zone.GetMemberChunksSnapshot()", StringComparison.Ordinal)
            && stockpileOverlayText.Contains("zone.GetMemberChunksSnapshot()", StringComparison.Ordinal)
            && stockpileDetailText.Contains("zone.GetMemberChunksSnapshot()", StringComparison.Ordinal)
            && zoneDetailText.Contains("zone.GetMemberChunksSnapshot().Count", StringComparison.Ordinal)
            && !zoneManagerText.Contains("_definitions.Values.ToList()", StringComparison.Ordinal)
            && !zoneManagerText.Contains("_zones.Values.ToList()", StringComparison.Ordinal)
            && !chunkZoneDataText.Contains("_shards.Values.ToList()", StringComparison.Ordinal)
            && !stockpileManagerText.Contains("_zones.Values.ToList()", StringComparison.Ordinal)
            && !chunkStockpileDataText.Contains("_shards.Values.ToList()", StringComparison.Ordinal),
            "Zone and stockpile owner snapshots must sort dictionary/list-backed data before exposing broad read APIs.");
        Console.WriteLine("[PASS] Zone and stockpile snapshots use stable ordering");
    }

    private static void TestTransportQueueShardSnapshotsUseStableOrdering(string root)
    {
        string queuePath = Path.Combine(root, "src", "HumanFortress.Simulation", "Jobs", "TransportRequestQueue.cs");
        string jobsDebugContractPath = Path.Combine(root, "src", "HumanFortress.Contracts", "Runtime", "Snapshots", "SimulationJobsDebugData.cs");
        string jobsTransportDebugPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Transport", "TransportDebugSnapshot.cs");
        string executorPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Transport", "TransportJobExecutor.Snapshots.cs");
        string runtimeBuilderPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "JobsDebugSnapshotBuilder.TransportDebug.cs");
        string text = File.ReadAllText(queuePath);
        string jobsDebugContractText = File.ReadAllText(jobsDebugContractPath);
        string jobsTransportDebugText = File.ReadAllText(jobsTransportDebugPath);
        string executorText = File.ReadAllText(executorPath);
        string runtimeBuilderText = File.ReadAllText(runtimeBuilderPath);

        RegressionAssert.True(
            text.Contains("_shards.OrderBy(static kv => kv.Key)", StringComparison.Ordinal)
            && text.Contains("Select(static kv => kv.Key)", StringComparison.Ordinal)
            && jobsDebugContractText.Contains("TransportShardCountView", StringComparison.Ordinal)
            && jobsDebugContractText.Contains("IReadOnlyList<TransportShardCountView> ShardCounts", StringComparison.Ordinal)
            && !jobsDebugContractText.Contains("IReadOnlyDictionary<int, int> ShardCounts", StringComparison.Ordinal)
            && jobsTransportDebugText.Contains("TransportShardCountDebugView", StringComparison.Ordinal)
            && jobsTransportDebugText.Contains("IReadOnlyList<TransportShardCountDebugView> ShardCounts", StringComparison.Ordinal)
            && executorText.Contains("OrderBy(static kv => kv.Key)", StringComparison.Ordinal)
            && executorText.Contains("new TransportShardCountDebugView(kv.Key, kv.Value)", StringComparison.Ordinal)
            && runtimeBuilderText.Contains("new TransportShardCountView(shard.ShardId, shard.Count)", StringComparison.Ordinal)
            && !text.Contains("return _shards.Keys.ToArray();", StringComparison.Ordinal)
            && !text.Contains("_shards.Keys", StringComparison.Ordinal),
            "Transport queue and Runtime debug shard snapshots must expose shard ids/counts as stable shard-id ordered rows.");
        Console.WriteLine("[PASS] Transport queue shard snapshots use stable ordering");
    }

    private static void TestChunkLifecycleHeatDecayUsesStableOrdering(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Simulation", "World", "ChunkLifecycleManager.cs");
        string text = File.ReadAllText(path);

        RegressionAssert.True(
            (text.Contains("OrderChunkKeys(_heatScores.Keys).ToArray()", StringComparison.Ordinal)
                || text.Contains("OrderChunkKeys(_heatScores.Select(static entry => entry.Key)).ToArray()", StringComparison.Ordinal))
            && text.Contains("OrderBy(static key => key.Z)", StringComparison.Ordinal)
            && !text.Contains("_heatScores.Keys.ToList()", StringComparison.Ordinal),
            "Chunk lifecycle heat decay must iterate heat-score chunk keys in stable spatial order.");
        Console.WriteLine("[PASS] Chunk lifecycle heat decay uses stable ordering");
    }

    private static void TestWorldAndPlaceableSnapshotsUseStableOrdering(string root)
    {
        string worldPath = Path.Combine(root, "src", "HumanFortress.Simulation", "World", "World.cs");
        string chunkPath = Path.Combine(root, "src", "HumanFortress.Simulation", "World", "Chunk.cs");
        string chunkPlaceableDataPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Placeables", "ChunkPlaceableData.cs");
        string affectedChunksPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Placeables", "PlaceableManager.AffectedChunks.cs");
        string placeableLookupPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Placeables", "PlaceableManager.Lookup.cs");
        string placeablePlacementPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Placeables", "PlaceableManager.Placement.cs");
        string placeableRemovalPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Placeables", "PlaceableManager.Removal.cs");
        string topologyTransactionPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Topology", "TopologyChangeTransaction.cs");
        string stockpileApplicatorPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Stockpile", "StockpileDiffApplicator.cs");
        string stockpileDetailPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "StockpileSnapshotBuilder.Detail.cs");
        string worldText = File.ReadAllText(worldPath);
        string chunkText = File.ReadAllText(chunkPath);
        string chunkPlaceableDataText = File.ReadAllText(chunkPlaceableDataPath);
        string affectedChunksText = File.ReadAllText(affectedChunksPath);
        string placeableLookupText = File.ReadAllText(placeableLookupPath);
        string placeablePlacementText = File.ReadAllText(placeablePlacementPath);
        string placeableRemovalText = File.ReadAllText(placeableRemovalPath);
        string topologyTransactionText = File.ReadAllText(topologyTransactionPath);
        string stockpileApplicatorText = File.ReadAllText(stockpileApplicatorPath);
        string stockpileDetailText = File.ReadAllText(stockpileDetailPath);

        RegressionAssert.True(
            worldText.Contains("return OrderChunks(_chunks.Values).ToArray();", StringComparison.Ordinal)
            && worldText.Contains("return OrderChunks(_chunks.Values.Where", StringComparison.Ordinal)
            && worldText.Contains("OrderChunkKeys(_dirtyChunks).ToList()", StringComparison.Ordinal)
            && worldText.Contains("foreach (var chunk in GetAllChunks())", StringComparison.Ordinal)
            && chunkText.Contains("SortedSet<int> _dirtyTiles", StringComparison.Ordinal)
            && chunkText.Contains("DrainDirtyTileIndices()", StringComparison.Ordinal)
            && chunkText.Contains("_dirtyTiles.ToArray()", StringComparison.Ordinal)
            && chunkPlaceableDataText.Contains("GetOwnedPlaceableSnapshot()", StringComparison.Ordinal)
            && chunkPlaceableDataText.Contains("GetExternalReferenceSnapshot()", StringComparison.Ordinal)
            && chunkPlaceableDataText.Contains("OrderBy(static entry => entry.Key)", StringComparison.Ordinal)
            && affectedChunksText.Contains("OrderBy(static chunk => chunk.Z)", StringComparison.Ordinal)
            && placeableLookupText.Contains("TryGetPlaceableAt(", StringComparison.Ordinal)
            && placeableLookupText.Contains("TryGetOwnedPlaceableByGuid", StringComparison.Ordinal)
            && placeableLookupText.Contains("data.TryGetExternalRefAt(localIndex", StringComparison.Ordinal)
            && placeablePlacementText.Contains("MarkFootprintCellsDirtyForChunk", StringComparison.Ordinal)
            && placeablePlacementText.Contains("TryValidatePlacement", StringComparison.Ordinal)
            && placeablePlacementText.Contains("TryAddExternalRef(cell.LocalIndex", StringComparison.Ordinal)
            && placeableRemovalText.Contains("TryValidateRemovalFootprint", StringComparison.Ordinal)
            && placeableRemovalText.Contains("RemoveDerivedFurniture", StringComparison.Ordinal)
            && topologyTransactionText.Contains("SortedDictionary<ChunkKey, SortedSet<int>>", StringComparison.Ordinal)
            && topologyTransactionText.Contains("chunk.CommitTopologyChange(entry.Value, _tick)", StringComparison.Ordinal)
            && topologyTransactionText.Contains("_world.MarkChunkDirty(entry.Key)", StringComparison.Ordinal)
            && !placeablePlacementText.Contains("MarkTileDirty(0", StringComparison.Ordinal)
            && stockpileApplicatorText.Contains(".OrderBy(static entry => entry.Key.Z)", StringComparison.Ordinal)
            && stockpileApplicatorText.Contains(".ThenBy(static entry => entry.Key.ChunkY)", StringComparison.Ordinal)
            && stockpileApplicatorText.Contains(".ThenBy(static entry => entry.Key.ChunkX)", StringComparison.Ordinal)
            && stockpileDetailText.Contains("TakeSorted(filter.Tags)", StringComparison.Ordinal)
            && stockpileDetailText.Contains("OrderBy(static value => value, StringComparer.Ordinal)", StringComparison.Ordinal)
            && !worldText.Contains("return _chunks.Values;", StringComparison.Ordinal)
            && !worldText.Contains("foreach (var chunk in _chunks.Values)", StringComparison.Ordinal)
            && !chunkText.Contains("DirtyTileSet) not yet implemented", StringComparison.Ordinal)
            && !chunkPlaceableDataText.Contains("resolve external refs via PlaceableManager", StringComparison.Ordinal)
            && !chunkPlaceableDataText.Contains("return _ownedPlaceables.Values;", StringComparison.Ordinal),
            "World/chunk/placeable owner snapshots must expose stable spatial/local-index ordering.");
        Console.WriteLine("[PASS] World and placeable snapshots use stable ordering");
    }

    private static void TestWorldSavePayloadRestoreUsesCanonicalOrdering(string root)
    {
        string restorerPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.cs");
        string validationPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.Validation.cs");
        string validationEntitiesPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.Validation.Entities.cs");
        string validationGeometryPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.Validation.Geometry.cs");
        string validationOrdersPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.Validation.Orders.cs");
        string validationStockpilesPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.Validation.Stockpiles.cs");
        string conversionPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.Conversion.cs");
        string placeablesPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.Placeables.cs");
        string runtimeManifestBuilderPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveManifestBuilder.cs");
        string runtimeManifestDataPath = Path.Combine(root, "src", "HumanFortress.Contracts", "Runtime", "Save", "RuntimeSaveManifestData.cs");
        string runtimeManifestSectionsPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveManifestSections.cs");
        string runtimeContentCatalogSummaryFactoryPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveContentCatalogSummaryFactory.cs");
        string runtimeContentSignatureFactoryPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveContentSignatureFactory.cs");
        string runtimeSlotManifestPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSlotManifest.cs");
        string runtimeSlotCompatibilityPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSlotCompatibilityPolicy.cs");
        string runtimeSlotContentCompatibilityPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSlotContentCompatibilityPolicy.cs");
        string runtimeSlotMigrationPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSlotMigrationPlanBuilder.cs");
        string runtimeSlotMigrationRegistryPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSlotMigrationTransformRegistry.cs");
        string runtimeSlotMigratorPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSlotMigrator.cs");
        string runtimeSlotRestorePlanPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSlotRestorePlanBuilder.cs");
        string runtimeJobStateRestorePolicyPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveJobStateRestorePolicy.cs");
        string runtimeStorePath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotDocumentStore.cs");
        string runtimeStoreInspectionPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotDocumentStore.Inspection.cs");
        string runtimeStoreIoPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotDocumentStore.IO.cs");
        string runtimeSavePortsPath = Path.Combine(root, "src", "HumanFortress.Runtime", "FortressRuntimeSessionPorts.Save.cs");
        string runtimeSaveCorePath = Path.Combine(root, "src", "HumanFortress.Runtime", "FortressRuntimeSessionCore.SaveSnapshot.cs");
        string runtimeSaveRestoreCorePath = Path.Combine(root, "src", "HumanFortress.Runtime", "FortressRuntimeSessionCore.SaveSnapshot.Restore.cs");
        string runtimeVerifierPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotDocumentVerifier.cs");
        string runtimeVerifierJobsPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotDocumentVerifier.Jobs.cs");
        string runtimeCraftMapperPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotDocumentCraftMapper.cs");
        string runtimeCraftRestorerPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotCraftJobRestorer.cs");
        string runtimeMiningMapperPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotDocumentMiningMapper.cs");
        string runtimeMiningRestorerPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotMiningJobRestorer.cs");
        string runtimeTransportMapperPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotDocumentTransportMapper.cs");
        string runtimeTransportRestorerPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotTransportJobRestorer.cs");
        string restorerText = File.ReadAllText(restorerPath);
        string validationText = File.ReadAllText(validationPath);
        string validationEntitiesText = File.ReadAllText(validationEntitiesPath);
        string validationGeometryText = File.ReadAllText(validationGeometryPath);
        string validationOrdersText = File.ReadAllText(validationOrdersPath);
        string validationStockpilesText = File.ReadAllText(validationStockpilesPath);
        string conversionText = File.ReadAllText(conversionPath);
        string placeablesText = File.ReadAllText(placeablesPath);
        string runtimeManifestBuilderText = File.ReadAllText(runtimeManifestBuilderPath);
        string runtimeManifestDataText = File.ReadAllText(runtimeManifestDataPath);
        string runtimeManifestSectionsText = File.ReadAllText(runtimeManifestSectionsPath);
        string runtimeContentCatalogSummaryFactoryText = File.ReadAllText(runtimeContentCatalogSummaryFactoryPath);
        string runtimeContentSignatureFactoryText = File.ReadAllText(runtimeContentSignatureFactoryPath);
        string runtimeSlotManifestText = File.ReadAllText(runtimeSlotManifestPath);
        string runtimeSlotCompatibilityText = File.ReadAllText(runtimeSlotCompatibilityPath);
        string runtimeSlotContentCompatibilityText = File.ReadAllText(runtimeSlotContentCompatibilityPath);
        string runtimeSlotMigrationText = File.ReadAllText(runtimeSlotMigrationPath);
        string runtimeSlotMigrationRegistryText = File.ReadAllText(runtimeSlotMigrationRegistryPath);
        string runtimeSlotMigratorText = File.ReadAllText(runtimeSlotMigratorPath);
        string runtimeSlotRestorePlanText = File.ReadAllText(runtimeSlotRestorePlanPath);
        string runtimeJobStateRestorePolicyText = File.ReadAllText(runtimeJobStateRestorePolicyPath);
        string runtimeStoreText = File.ReadAllText(runtimeStorePath);
        string runtimeStoreInspectionText = File.ReadAllText(runtimeStoreInspectionPath);
        string runtimeStoreIoText = File.ReadAllText(runtimeStoreIoPath);
        string runtimeSavePortsText = File.ReadAllText(runtimeSavePortsPath);
        string runtimeSaveCoreText = File.ReadAllText(runtimeSaveCorePath);
        string runtimeSaveRestoreCoreText = File.ReadAllText(runtimeSaveRestoreCorePath);
        string runtimeVerifierText = File.ReadAllText(runtimeVerifierPath);
        string runtimeVerifierJobsText = File.ReadAllText(runtimeVerifierJobsPath);
        string runtimeCraftMapperText = File.ReadAllText(runtimeCraftMapperPath);
        string runtimeCraftRestorerText = File.ReadAllText(runtimeCraftRestorerPath);
        string runtimeMiningMapperText = File.ReadAllText(runtimeMiningMapperPath);
        string runtimeMiningRestorerText = File.ReadAllText(runtimeMiningRestorerPath);
        string runtimeTransportMapperText = File.ReadAllText(runtimeTransportMapperPath);
        string runtimeTransportRestorerText = File.ReadAllText(runtimeTransportRestorerPath);
        int placeablePreflightIndex = restorerText.IndexOf(
            "ValidatePlaceablesSnapshot(world, payload.Placeables)",
            StringComparison.Ordinal);
        int itemRestoreIndex = restorerText.IndexOf(
            "world.Items.SetDependencies(world)",
            StringComparison.Ordinal);
        const string managerRestoreFailureGate =
            "if (issues.Count > 0)\n                return FailedAfterPartialRestore(payload, world, issues);\n\n            RestoreValidatedPlaceablesSnapshot";

        RegressionAssert.True(
            restorerText.Contains(".OrderBy(static chunk => chunk.Z)", StringComparison.Ordinal)
            && restorerText.Contains("ThenBy(static chunk => chunk.ChunkY)", StringComparison.Ordinal)
            && restorerText.Contains("ThenBy(static chunk => chunk.ChunkX)", StringComparison.Ordinal)
            && placeablePreflightIndex >= 0
            && itemRestoreIndex > placeablePreflightIndex
            && restorerText.Contains("RestoreValidatedPlaceablesSnapshot(world, payload.Placeables!)", StringComparison.Ordinal)
            && restorerText.Contains(managerRestoreFailureGate, StringComparison.Ordinal)
            && validationText.Contains("ValidateUniquePayloadIds", StringComparison.Ordinal)
            && validationText.Contains("payload.Items", StringComparison.Ordinal)
            && validationText.Contains("payload.Creatures", StringComparison.Ordinal)
            && validationText.Contains("duplicates guid", StringComparison.Ordinal)
            && validationText.Contains("MinSizeInChunks", StringComparison.Ordinal)
            && validationText.Contains("MaxSizeInChunks", StringComparison.Ordinal)
            && validationText.Contains("ValidateStockpileZonePayloads", StringComparison.Ordinal)
            && validationText.Contains("ValidateOrderPayloads", StringComparison.Ordinal),
            "World payload restore must preflight all owner slices before mutating authority.");

        RegressionAssert.True(
            validationEntitiesText.Contains("ValidateSupportedItemSlice", StringComparison.Ordinal)
            && validationEntitiesText.Contains("ValidateReservationReferences", StringComparison.Ordinal)
            && validationEntitiesText.Contains("ValidateContainedItemLocation", StringComparison.Ordinal)
            && validationEntitiesText.Contains("ValidateContainedItemGraph", StringComparison.Ordinal)
            && validationEntitiesText.Contains("ValidateCreatureOwnedItemLocation", StringComparison.Ordinal)
            && validationEntitiesText.Contains("ValidateInstalledItemLocation", StringComparison.Ordinal)
            && validationEntitiesText.Contains("ValidateItemReservationTokens", StringComparison.Ordinal)
            && validationEntitiesText.Contains("references missing containing item", StringComparison.Ordinal)
            && validationEntitiesText.Contains("contained-item cycle", StringComparison.Ordinal)
            && validationEntitiesText.Contains("references missing claimant creature", StringComparison.Ordinal)
            && validationEntitiesText.Contains("reservation tokens reserve", StringComparison.Ordinal)
            && validationEntitiesText.Contains("duplicates reservation token identity", StringComparison.Ordinal)
            && validationEntitiesText.Contains("references missing item", StringComparison.Ordinal)
            && validationEntitiesText.Contains("references missing creature", StringComparison.Ordinal),
            "World payload entity validation must reject invalid ownership and reservation graphs.");

        RegressionAssert.True(
            validationStockpilesText.Contains("ValidateStockpileZonePayloads", StringComparison.Ordinal)
            && validationStockpilesText.Contains("duplicates zone id", StringComparison.Ordinal)
            && validationStockpilesText.Contains("duplicates chunk", StringComparison.Ordinal)
            && validationOrdersText.Contains("ValidateOrderPayloads", StringComparison.Ordinal)
            && validationOrdersText.Contains("duplicates mining id", StringComparison.Ordinal)
            && validationOrdersText.Contains("ValidateWorldRectangle", StringComparison.Ordinal)
            && validationGeometryText.Contains("ValidateWorldRectangle", StringComparison.Ordinal)
            && validationGeometryText.Contains("ValidateWorldChunkKey", StringComparison.Ordinal)
            && validationGeometryText.Contains("ValidateStringArray", StringComparison.Ordinal)
            && conversionText.Contains("rows.OrderBy(static row => row.Key, StringComparer.Ordinal)", StringComparison.Ordinal)
            && conversionText.Contains("OrderBy(improvement => improvement.Type, StringComparer.Ordinal)", StringComparison.Ordinal)
            && conversionText.Contains("ThenBy(improvement => improvement.Description, StringComparer.Ordinal)", StringComparison.Ordinal),
            "World payload stockpile, order, geometry, and conversion paths must validate and sort canonically.");

        RegressionAssert.True(
            placeablesText.Contains("ValidatePlaceablesSnapshot", StringComparison.Ordinal)
            && placeablesText.Contains("RestoreValidatedPlaceablesSnapshot", StringComparison.Ordinal)
            && placeablesText.Contains("OrderBy(placeable => placeable.Guid)", StringComparison.Ordinal)
            && placeablesText.Contains("ThenBy(placeable => placeable.OwnerLocalIndex)", StringComparison.Ordinal),
            "World payload placeables must validate first and restore in canonical owner order.");

        RegressionAssert.True(
            runtimeVerifierText.Contains("ValidateManifestSections(document, issues)", StringComparison.Ordinal)
            && runtimeManifestSectionsText.Contains("internal static class RuntimeSaveManifestSections", StringComparison.Ordinal)
            && runtimeManifestSectionsText.Contains("RuntimeSaveManifestSectionDefinition", StringComparison.Ordinal)
            && runtimeManifestSectionsText.Contains("Array.AsReadOnly", StringComparison.Ordinal)
            && runtimeManifestSectionsText.Contains("internal static IEnumerable<string> OrderedNames", StringComparison.Ordinal)
            && runtimeManifestSectionsText.Contains("internal static bool TryGetRequirement", StringComparison.Ordinal)
            && runtimeManifestBuilderText.Contains("RuntimeSaveManifestSections.WorldTerrain", StringComparison.Ordinal)
            && runtimeManifestBuilderText.Contains("RuntimeSaveManifestSections.TryGetRequirement", StringComparison.Ordinal)
            && runtimeManifestDataText.Contains("public const int CurrentVersion = 6;", StringComparison.Ordinal)
            && runtimeManifestDataText.Contains("RuntimeSaveContentCatalogSummaryData ContentCatalog", StringComparison.Ordinal)
            && runtimeManifestBuilderText.Contains("RuntimeSaveContentSignatureFactory.FromRuntimeContent(content)", StringComparison.Ordinal)
            && runtimeManifestBuilderText.Contains("RuntimeSaveContentCatalogSummaryFactory.FromRuntimeContent(content)", StringComparison.Ordinal)
            && runtimeContentCatalogSummaryFactoryText.Contains("internal static class RuntimeSaveContentCatalogSummaryFactory", StringComparison.Ordinal)
            && runtimeContentCatalogSummaryFactoryText.Contains("RuntimeSaveContentCatalogSummaryData.Unavailable", StringComparison.Ordinal)
            && runtimeContentCatalogSummaryFactoryText.Contains("content.Materials.GetNameToIdSnapshot()", StringComparison.Ordinal)
            && runtimeContentCatalogSummaryFactoryText.Contains("content.TerrainKinds.GetAllKinds()", StringComparison.Ordinal)
            && runtimeContentCatalogSummaryFactoryText.Contains("content.Constructions.GetAllConstructions()", StringComparison.Ordinal)
            && runtimeContentCatalogSummaryFactoryText.Contains("content.Recipes.GetAllRecipes()", StringComparison.Ordinal)
            && runtimeContentCatalogSummaryFactoryText.Contains(".OrderBy(static id => id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && runtimeContentSignatureFactoryText.Contains("FortressRuntimeContentSnapshot?", StringComparison.Ordinal)
            && runtimeContentSignatureFactoryText.Contains("RuntimeSaveContentSignatureData.Unavailable", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("RuntimeSaveManifestSections.TryGetRequirement", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("RuntimeSaveManifestSections.OrderedNames", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("ValidateContentCatalog(document.Manifest.Content, document.Manifest.ContentCatalog, issues)", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("if (!catalog.HasCatalog)\n            return;", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("\"manifest.content_catalog\"", StringComparison.Ordinal),
            "Runtime manifest and content catalog guards must preserve canonical content ordering.");

        RegressionAssert.True(
            runtimeSlotManifestText.Contains("RuntimeSaveSlotManifestBuilder", StringComparison.Ordinal)
            && runtimeSlotManifestText.Contains("RuntimeSaveSlotManifestVerifier", StringComparison.Ordinal)
            && runtimeSlotManifestText.Contains("RuntimeSaveSlotCompatibilityPolicy.Evaluate(slotManifest)", StringComparison.Ordinal)
            && runtimeSlotManifestText.Contains("slot.manifest", StringComparison.Ordinal)
            && runtimeSlotCompatibilityText.Contains("internal static class RuntimeSaveSlotCompatibilityPolicy", StringComparison.Ordinal)
            && runtimeSlotCompatibilityText.Contains("EvaluateLegacySnapshotDocument", StringComparison.Ordinal)
            && runtimeSlotCompatibilityText.Contains("RuntimeSaveSlotCompatibilityStatus.MigrationRequired", StringComparison.Ordinal)
            && runtimeSlotCompatibilityText.Contains("\"slot.compatibility\"", StringComparison.Ordinal)
            && runtimeSlotCompatibilityText.Contains("CanRead: false", StringComparison.Ordinal)
            && runtimeSlotCompatibilityText.Contains("RequiresMigration: true", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("internal static class RuntimeSaveSlotContentCompatibilityPolicy", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("RuntimeSaveSlotContentCompatibilityStatus.ContentHashMismatch", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("RuntimeSaveSlotContentCompatibilityStatus.CatalogShapeMismatch", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("RequiresMissingContentPolicy: true", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("BuildDifferenceDetails", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("RuntimeSaveContentCompatibilityDifferenceKind.MaterialCount", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("DifferenceDetails: differenceDetails", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("HasSavedCatalogKeys: false", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("HasCurrentCatalogKeys: HasCatalogKeys(kind, currentCatalog)", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("MissingCurrentKeys: BuildMissingCurrentKeys(kind, savedCatalog, currentCatalog)", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("AdditionalCurrentKeys: BuildAdditionalCurrentKeys(kind, savedCatalog, currentCatalog)", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("RuntimeSaveContentCatalogSummaryFactory.FromRuntimeContent(currentContent)", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("SavedCatalog: savedCatalog", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("CurrentCatalog: currentCatalog", StringComparison.Ordinal)
            && runtimeSlotContentCompatibilityText.Contains("\"slot.content\"", StringComparison.Ordinal)
            && runtimeSlotMigrationText.Contains("internal static class RuntimeSaveSlotMigrationPlanBuilder", StringComparison.Ordinal)
            && runtimeSlotMigrationText.Contains("RuntimeSaveSlotMigrationPlanData", StringComparison.Ordinal)
            && runtimeSlotMigrationText.Contains("RuntimeSaveSlotMigrationTransformRegistry.BuildRequiredTransformIds", StringComparison.Ordinal)
            && runtimeSlotMigrationText.Contains("RuntimeSaveSlotMigrationTransformRegistry.CanSatisfy", StringComparison.Ordinal)
            && runtimeSlotMigrationText.Contains("IsExpectedSlotTransformInputIssue", StringComparison.Ordinal)
            && runtimeSlotMigrationText.Contains("IsExpectedRuntimeSnapshotTransformInputIssue", StringComparison.Ordinal)
            && runtimeSlotMigrationText.Contains("\"slot.migration\"", StringComparison.Ordinal)
            && runtimeSlotMigrationText.Contains("CanMigrate: canMigrate", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("internal static class RuntimeSaveSlotMigrationTransformRegistry", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("LegacySnapshotOnlySlotTransformId = \"slot:0->1\"", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("RuntimeSnapshot4To5TransformId = \"runtime_snapshot:4->5\"", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("RuntimeSnapshot5To6TransformId = \"runtime_snapshot:5->6\"", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("version < RuntimeSaveFormat.CurrentVersion", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("CreateRuntimeSnapshotTransformId(\n                    version,\n                    version + 1)", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("return transforms.ToArray();", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("var appliedTransforms = new List<string>()", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("appliedTransforms.Add(transformId)", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("AppliedTransforms: appliedTransforms.ToArray()", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("ApplyLegacySnapshotOnlySlotTransform", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("ApplyRuntimeSnapshotTransforms", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("ApplyRuntimeSnapshotTransform", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("RuntimeSaveSnapshotDocumentVerifier.Validate(migratedDocument)", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("RuntimeSaveSnapshotDocumentStore.WriteAtomic(targetDirectory, document)", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("while (migratedDocument.Manifest.FormatVersion < RuntimeSaveFormat.CurrentVersion)", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("CreateMissingTransformsMessage", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("Missing transforms", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("slot:", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("runtime_snapshot:", StringComparison.Ordinal)
            && runtimeSlotMigrationRegistryText.Contains("ApplyTransforms", StringComparison.Ordinal)
            && runtimeSlotMigratorText.Contains("internal static class RuntimeSaveSlotMigrator", StringComparison.Ordinal)
            && runtimeSlotMigratorText.Contains("RuntimeSaveSnapshotDocumentStore.InspectDirectory(sourceDirectory)", StringComparison.Ordinal)
            && runtimeSlotMigratorText.Contains("inspection.MigrationPlan.CanMigrate", StringComparison.Ordinal)
            && runtimeSlotMigratorText.Contains("RuntimeSaveSlotMigrationTransformRegistry.ApplyTransforms", StringComparison.Ordinal)
            && runtimeSlotMigratorText.Contains("RuntimeSaveSlotMigrationResultData", StringComparison.Ordinal)
            && runtimeSlotMigratorText.Contains("\"slot.migration\"", StringComparison.Ordinal)
            && runtimeSlotRestorePlanText.Contains("internal static class RuntimeSaveSlotRestorePlanBuilder", StringComparison.Ordinal)
            && runtimeSlotRestorePlanText.Contains("CanRestorePendingCommands", StringComparison.Ordinal)
            && runtimeSlotRestorePlanText.Contains("CanRestoreWorld", StringComparison.Ordinal)
            && runtimeSlotRestorePlanText.Contains("CanRestoreFull", StringComparison.Ordinal)
            && runtimeSlotRestorePlanText.Contains("contentCompatibility.CanBindContent", StringComparison.Ordinal)
            && runtimeSlotRestorePlanText.Contains("RuntimeSaveSlotContentCompatibilityPolicy.CreateBlockingIssue", StringComparison.Ordinal)
            && runtimeSlotRestorePlanText.Contains("\"slot.restore_plan\"", StringComparison.Ordinal)
            && runtimeSlotRestorePlanText.Contains("RuntimeSaveJobStateRestorePolicy.GetPresentUnsupportedSections", StringComparison.Ordinal),
            "Runtime slot compatibility, migration, and restore-plan guards must remain explicit.");

        RegressionAssert.True(
            runtimeJobStateRestorePolicyText.Contains("internal static class RuntimeSaveJobStateRestorePolicy", StringComparison.Ordinal)
            && runtimeJobStateRestorePolicyText.Contains("RuntimeSaveManifestSections.JobsTransport", StringComparison.Ordinal)
            && runtimeJobStateRestorePolicyText.Contains("RuntimeSaveManifestSections.JobsMining", StringComparison.Ordinal)
            && runtimeJobStateRestorePolicyText.Contains("RuntimeSaveManifestSections.JobsCraft", StringComparison.Ordinal)
            && runtimeJobStateRestorePolicyText.Contains("HasSupportedTransportPayload", StringComparison.Ordinal)
            && runtimeJobStateRestorePolicyText.Contains("HasSupportedMiningPayload", StringComparison.Ordinal)
            && runtimeJobStateRestorePolicyText.Contains("HasSupportedCraftPayload", StringComparison.Ordinal)
            && runtimeJobStateRestorePolicyText.Contains("RuntimeSaveSnapshotDocumentTransportMapper.CountRecords", StringComparison.Ordinal)
            && runtimeJobStateRestorePolicyText.Contains("RuntimeSaveSnapshotDocumentMiningMapper.CountRecords", StringComparison.Ordinal)
            && runtimeJobStateRestorePolicyText.Contains("RuntimeSaveSnapshotDocumentCraftMapper.CountRecords", StringComparison.Ordinal)
            && runtimeJobStateRestorePolicyText.Contains("job-state checkpoint sections", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("ValidateMiningJobs(document, issues)", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("ValidateTransportJobs(document, issues)", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("ValidateCraftJobs(document, issues)", StringComparison.Ordinal)
            && runtimeVerifierJobsText.Contains("RuntimeSaveSnapshotDocumentMiningMapper.BuildReplayHash", StringComparison.Ordinal)
            && runtimeVerifierJobsText.Contains("RuntimeSaveSnapshotDocumentTransportMapper.BuildReplayHash", StringComparison.Ordinal)
            && runtimeVerifierJobsText.Contains("RuntimeSaveSnapshotDocumentCraftMapper.BuildReplayHash", StringComparison.Ordinal)
            && runtimeVerifierJobsText.Contains("Mining job manifest section is non-empty", StringComparison.Ordinal)
            && runtimeVerifierJobsText.Contains("Transport job manifest section is non-empty", StringComparison.Ordinal)
            && runtimeVerifierJobsText.Contains("Craft job manifest section is non-empty", StringComparison.Ordinal)
            && runtimeMiningMapperText.Contains("MiningReplayHashBuilder.Build", StringComparison.Ordinal)
            && runtimeMiningMapperText.Contains("ValidateEnum<MiningStage>", StringComparison.Ordinal)
            && runtimeMiningMapperText.Contains("ValidateEnum<TerrainKind>", StringComparison.Ordinal)
            && runtimeMiningMapperText.Contains("ValidateEnum<MiningAction>", StringComparison.Ordinal)
            && runtimeMiningRestorerText.Contains("RestoreReplaySnapshot(snapshot)", StringComparison.Ordinal)
            && runtimeMiningRestorerText.Contains("RuntimeSaveManifestSections.JobsMining", StringComparison.Ordinal)
            && runtimeCraftMapperText.Contains("CraftReplayHashBuilder.Build", StringComparison.Ordinal)
            && runtimeCraftMapperText.Contains("ValidateEnum<CraftJobStage>", StringComparison.Ordinal)
            && runtimeCraftRestorerText.Contains("RestoreReplaySnapshot(snapshot)", StringComparison.Ordinal)
            && runtimeCraftRestorerText.Contains("RuntimeSaveManifestSections.JobsCraft", StringComparison.Ordinal)
            && runtimeTransportMapperText.Contains("TransportReplayHashBuilder.Build", StringComparison.Ordinal)
            && runtimeTransportMapperText.Contains("ValidateEnum<TransportReason>", StringComparison.Ordinal)
            && runtimeTransportMapperText.Contains("ValidateEnum<JobStage>", StringComparison.Ordinal)
            && runtimeTransportRestorerText.Contains("RestoreReplaySnapshot(queue, executor)", StringComparison.Ordinal)
            && runtimeTransportRestorerText.Contains("RuntimeSaveManifestSections.JobsTransport", StringComparison.Ordinal),
            "Runtime job checkpoint guards must validate and restore every supported job section.");

        RegressionAssert.True(
            runtimeStoreText.Contains("SlotManifestFileName", StringComparison.Ordinal)
            && runtimeStoreText.Contains("RuntimeSaveSlotManifestBuilder.Build(document)", StringComparison.Ordinal)
            && runtimeStoreText.Contains("return InspectDirectory(directory).Validation;", StringComparison.Ordinal)
            && runtimeStoreText.Contains("ValidateDirectory(string directory)", StringComparison.Ordinal)
            && !runtimeStoreText.Contains("new RuntimeSaveSlotInspectionData", StringComparison.Ordinal)
            && runtimeStoreInspectionText.Contains("InspectDirectory(", StringComparison.Ordinal)
            && runtimeStoreInspectionText.Contains("new RuntimeSaveSlotInspectionData", StringComparison.Ordinal)
            && runtimeStoreInspectionText.Contains("RuntimeSaveSlotContentCompatibilityPolicy.Evaluate", StringComparison.Ordinal)
            && runtimeStoreInspectionText.Contains("RuntimeSaveSlotMigrationPlanBuilder.Build", StringComparison.Ordinal)
            && runtimeStoreInspectionText.Contains("RuntimeSaveSlotRestorePlanBuilder.Build", StringComparison.Ordinal)
            && runtimeStoreInspectionText.Contains("CombineValidation", StringComparison.Ordinal),
            "Runtime snapshot store inspection must own validation and compatibility composition.");

        RegressionAssert.True(
            runtimeStoreIoText.Contains("ReadUnchecked(string directory)", StringComparison.Ordinal)
            && runtimeStoreIoText.Contains("ValidateSlotManifestNoThrow(directory, document)", StringComparison.Ordinal)
            && runtimeStoreIoText.Contains("WriteTextDurably", StringComparison.Ordinal)
            && runtimeStoreIoText.Contains("BuildDirectoryFailure", StringComparison.Ordinal)
            && runtimeSavePortsText.Contains("RuntimeSaveSlotInspectionData InspectSaveSnapshotDirectory(string directory)", StringComparison.Ordinal)
            && runtimeSavePortsText.Contains("RuntimeSaveSlotMigrationResultData MigrateSaveSnapshotDirectory", StringComparison.Ordinal)
            && runtimeSaveCoreText.Contains("RuntimeSaveSnapshotDocumentStore.InspectDirectory(directory, _runtimeContentSnapshot)", StringComparison.Ordinal)
            && runtimeSaveCoreText.Contains("RestorePendingCommandsFromSaveSnapshotDocumentCore", StringComparison.Ordinal)
            && runtimeSaveCoreText.Contains("RuntimeSaveSlotMigrator.MigrateDirectory(sourceDirectory, targetDirectory)", StringComparison.Ordinal)
            && runtimeSaveRestoreCoreText.Contains("RestorePendingCommandsFromSaveSnapshotDocumentCore", StringComparison.Ordinal)
            && runtimeSaveRestoreCoreText.Contains("EvaluateSaveContentCompatibility", StringComparison.Ordinal)
            && runtimeSaveRestoreCoreText.Contains("RuntimeSaveSlotContentCompatibilityPolicy.CreateBlockingIssue", StringComparison.Ordinal)
            && runtimeSaveRestoreCoreText.Contains("RuntimeSaveJobStateRestorePolicy.GetPresentUnsupportedSections", StringComparison.Ordinal),
            "Runtime snapshot IO and internal session ports must preserve their validation boundaries.");

        RegressionAssert.True(
            runtimeVerifierText.Contains("new HashSet<string>(StringComparer.Ordinal)", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("Manifest section duplicates", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("is not recognized by this runtime", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("has a negative record count", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("is absent but still has a hash", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("is absent but still has a record count", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("is missing", StringComparison.Ordinal),
            "Runtime snapshot manifest verification must reject malformed section metadata.");
        Console.WriteLine("[PASS] World save payload restore uses canonical ordering");
    }

    private static void TestConstructionMaterialSnapshotsUseStableOrdering(string root)
    {
        string constructionSitePath = Path.Combine(root, "src", "HumanFortress.Simulation", "Placeables", "ConstructionSiteState.cs");
        string constructionRequirementPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Placeables", "ConstructionMaterialRequirement.cs");
        string buildableConstructionPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Orders", "BuildableConstructionSystem.cs");
        string trackerPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Construction", "ConstructionMaterialTracker.cs");
        string matcherPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Construction", "ConstructionRequirementMatcher.cs");
        string plannerPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Jobs", "ConstructionMaterialsPlanner.cs");
        string progressPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Construction", "ConstructionSiteProgress.cs");
        string coordinatorPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Construction", "ConstructionCompletionCoordinator.cs");
        string completionPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Construction", "ConstructionCompletionApplier.cs");
        string workshopMaterialsPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "WorkshopSnapshotBuilder.Materials.cs");
        string payloadBuilderPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadBuilder.Placeables.cs");
        string replayHashPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Replay", "PlaceablesReplayHashBuilder.cs");
        string constructionSiteText = File.ReadAllText(constructionSitePath);
        string constructionRequirementText = File.ReadAllText(constructionRequirementPath);
        string buildableConstructionText = File.ReadAllText(buildableConstructionPath);
        string trackerText = File.ReadAllText(trackerPath);
        string matcherText = File.ReadAllText(matcherPath);
        string plannerText = File.ReadAllText(plannerPath);
        string progressText = File.ReadAllText(progressPath);
        string coordinatorText = File.ReadAllText(coordinatorPath);
        string completionText = File.ReadAllText(completionPath);
        string workshopMaterialsText = File.ReadAllText(workshopMaterialsPath);
        string payloadBuilderText = File.ReadAllText(payloadBuilderPath);
        string replayHashText = File.ReadAllText(replayHashPath);

        RegressionAssert.True(
            constructionSiteText.Contains("GetRequiredMaterialsSnapshot", StringComparison.Ordinal)
            && constructionSiteText.Contains("GetRequiredMaterialIdsSnapshot", StringComparison.Ordinal)
            && constructionSiteText.Contains("GetDeliveredMaterialsSnapshot", StringComparison.Ordinal)
            && constructionSiteText.Contains("OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)", StringComparison.Ordinal)
            && constructionSiteText.Contains("ThenBy(static entry => entry.Key, StringComparer.Ordinal)", StringComparison.Ordinal)
            && constructionRequirementText.Contains("DefinitionPrefix = \"def:\"", StringComparison.Ordinal)
            && constructionRequirementText.Contains("MatchesItem(ItemDefinition definition, string requirement)", StringComparison.Ordinal)
            && buildableConstructionText.Contains("BuildMaterialRequirements(def)", StringComparison.Ordinal)
            && buildableConstructionText.Contains("ConstructionMaterialRequirement.ForDefinition", StringComparison.Ordinal)
            && !buildableConstructionText.Contains("defId support in planner", StringComparison.Ordinal)
            && trackerText.Contains("GetRequiredMaterialIdsSnapshot()", StringComparison.Ordinal)
            && matcherText.Contains("ConstructionMaterialRequirement.MatchesItem(definition, requirement)", StringComparison.Ordinal)
            && plannerText.Contains("site.GetRequiredMaterialsSnapshot()", StringComparison.Ordinal)
            && plannerText.Contains("GetRequiredMaterialIdsSnapshot()", StringComparison.Ordinal)
            && plannerText.Contains("TryFindNearestItemForRequirement", StringComparison.Ordinal)
            && plannerText.Contains("ConstructionMaterialRequirement.MatchesItem(def", StringComparison.Ordinal)
            && progressText.Contains("construction.GetRequiredMaterialsSnapshot()", StringComparison.Ordinal)
            && coordinatorText.Contains("construction.GetRequiredMaterialsSnapshot()", StringComparison.Ordinal)
            && progressText.Contains("OrderBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase)", StringComparison.Ordinal)
            && completionText.Contains("OrderBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase)", StringComparison.Ordinal)
            && completionText.Contains("construction.GetRequiredMaterialsSnapshot()", StringComparison.Ordinal)
            && workshopMaterialsText.Contains("GetRequiredMaterialIdsSnapshot()", StringComparison.Ordinal)
            && workshopMaterialsText.Contains("construction.GetRequiredMaterialsSnapshot()", StringComparison.Ordinal)
            && payloadBuilderText.Contains("construction.GetRequiredMaterialsSnapshot()", StringComparison.Ordinal)
            && payloadBuilderText.Contains("construction.GetDeliveredMaterialsSnapshot()", StringComparison.Ordinal)
            && replayHashText.Contains("construction.GetRequiredMaterialsSnapshot()", StringComparison.Ordinal)
            && replayHashText.Contains("construction.GetDeliveredMaterialsSnapshot()", StringComparison.Ordinal)
            && !constructionSiteText.Contains("MaterialsRequired.Keys", StringComparison.Ordinal)
            && !trackerText.Contains("MaterialsRequired.Keys", StringComparison.Ordinal)
            && !plannerText.Contains("MaterialsRequired.Keys", StringComparison.Ordinal)
            && !workshopMaterialsText.Contains("MaterialsRequired.Keys", StringComparison.Ordinal),
            "Construction material requirement/delivery readers must sort through ConstructionSiteState owner APIs.");
        Console.WriteLine("[PASS] Construction material snapshots use stable ordering");
    }

    private static void TestReservationManagerUsesWritePhaseDictionarySemantics(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Simulation", "Jobs", "ReservationManager.cs");
        string text = File.ReadAllText(path);

        RegressionAssert.True(
            !text.Contains("ConcurrentDictionary", StringComparison.Ordinal)
            && !text.Contains("AddOrUpdate", StringComparison.Ordinal)
            && !text.Contains(".TryRemove(", StringComparison.Ordinal)
            && text.Contains("Dictionary<Guid, ItemReservation>", StringComparison.Ordinal)
            && text.Contains("Dictionary<Guid, CreatureReservation>", StringComparison.Ordinal)
            && text.Contains("private readonly object _sync = new();", StringComparison.Ordinal)
            && text.Contains("TryReleaseItem(ItemToken token)", StringComparison.Ordinal)
            && text.Contains("TryReleaseCreature(CreatureToken token)", StringComparison.Ordinal)
            && text.Contains("!existing.Token.Matches(token)", StringComparison.Ordinal)
            && CountOccurrences(text, "OrderBy(static entry => entry.Key)") >= 2,
            "ReservationManager should remain owner-locked, token-CAS state instead of callback-driven concurrent mutation.");
        Console.WriteLine("[PASS] ReservationManager uses owner-locked token-CAS semantics");
    }

    private static void TestOrdersManagerUsesDeterministicOwnedCollections(string root)
    {
        string ordersPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Orders");
        string[] files = Directory.GetFiles(ordersPath, "OrdersManager*.cs")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string text = string.Join('\n', files.Select(File.ReadAllText));

        RegressionAssert.True(
            !text.Contains("ConcurrentBag", StringComparison.Ordinal)
            && !text.Contains("ConcurrentQueue", StringComparison.Ordinal)
            && !text.Contains("Interlocked", StringComparison.Ordinal)
            && text.Contains("private readonly object _sync = new();", StringComparison.Ordinal)
            && text.Contains("Queue<HaulDesignation> _haulQueue", StringComparison.Ordinal)
            && text.Contains("Queue<MiningDesignation> _miningAdd", StringComparison.Ordinal)
            && text.Contains("Queue<ConstructionDesignation> _constructionQueue", StringComparison.Ordinal)
            && text.Contains("Queue<BuildableConstructionDesignation> _buildableQueue", StringComparison.Ordinal)
            && text.Contains("List<HaulDesignation> _activeHauls", StringComparison.Ordinal)
            && text.Contains("List<MiningDesignation> _activeMining", StringComparison.Ordinal)
            && text.Contains("List<ConstructionDesignation> _activeConstruction", StringComparison.Ordinal)
            && text.Contains("List<BuildableConstructionDesignation> _activeBuildable", StringComparison.Ordinal)
            && text.Contains("return OrderHauls(_activeHauls).ToList();", StringComparison.Ordinal)
            && text.Contains("return OrderMining(_activeMining).ToList();", StringComparison.Ordinal)
            && text.Contains("return OrderConstruction(_activeConstruction).ToList();", StringComparison.Ordinal)
            && text.Contains("return OrderBuildable(_activeBuildable).ToList();", StringComparison.Ordinal),
            "OrdersManager should keep authoritative order queues/lists as deterministic owner state, not concurrent collection enumeration.");
        Console.WriteLine("[PASS] OrdersManager uses deterministic owned collections");
    }

    private static void TestOrderPlannerOutboxesUseDeterministicOwnerQueues(string root)
    {
        string ordersPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Orders");
        string miningPath = Path.Combine(ordersPath, "MiningSystem.cs");
        string miningTickPath = Path.Combine(ordersPath, "MiningSystem.Tick.cs");
        string constructionPath = Path.Combine(ordersPath, "ConstructionSystem.cs");
        string buildablePath = Path.Combine(ordersPath, "BuildableConstructionSystem.cs");
        string miningText = File.ReadAllText(miningPath);
        string miningTickText = File.ReadAllText(miningTickPath);
        string constructionText = File.ReadAllText(constructionPath);
        string buildableText = File.ReadAllText(buildablePath);

        RegressionAssert.True(
            !miningText.Contains("ConcurrentQueue", StringComparison.Ordinal)
            && miningText.Contains("Queue<PlannedDig> _outbox", StringComparison.Ordinal)
            && miningTickText.Contains("_outbox.Enqueue(plannedDig)", StringComparison.Ordinal)
            && miningTickText.Contains("_outbox.TryDequeue(out var plannedDig)", StringComparison.Ordinal)
            && !constructionText.Contains("ConcurrentQueue", StringComparison.Ordinal)
            && !constructionText.Contains("_outbox", StringComparison.Ordinal)
            && !constructionText.Contains("DequeuePlannedBuilds", StringComparison.Ordinal)
            && !buildableText.Contains("ConcurrentQueue", StringComparison.Ordinal)
            && buildableText.Contains("Queue<PlaceableInstance> _outbox", StringComparison.Ordinal)
            && buildableText.Contains("_outbox.TryDequeue(out var site)", StringComparison.Ordinal),
            "Order planner outboxes should be deterministic owner queues, and dead construction planned-build outboxes should stay removed.");
        Console.WriteLine("[PASS] Order planner outboxes use deterministic owner queues");
    }

    private static void TestJobsBacklogsUseDeterministicOwnerQueues(string root)
    {
        string jobsPath = Path.Combine(root, "src", "HumanFortress.Jobs");
        var files = new[]
        {
            Path.Combine(jobsPath, "Mining", "MiningBacklogBuffer.cs"),
            Path.Combine(jobsPath, "Transport", "TransportBacklogBuffer.cs"),
            Path.Combine(jobsPath, "Craft", "CraftPlanner.cs"),
            Path.Combine(jobsPath, "Craft", "CraftJobExecutor.cs"),
            Path.Combine(jobsPath, "Craft", "CraftJobExecutor.Restore.cs")
        };

        var textByFile = files.ToDictionary(
            file => TestRepositoryPaths.RelativePath(root, file),
            File.ReadAllText,
            StringComparer.Ordinal);
        string combined = string.Join('\n', textByFile.Values);

        RegressionAssert.True(
            !combined.Contains("ConcurrentQueue", StringComparison.Ordinal)
            && !combined.Contains("System.Collections.Concurrent", StringComparison.Ordinal)
            && textByFile.Values.Any(static text => text.Contains("Queue<BacklogEntry> _queue", StringComparison.Ordinal))
            && textByFile.Values.Any(static text => text.Contains("Queue<TransportRequest> _queue", StringComparison.Ordinal))
            && textByFile.Values.Any(static text => text.Contains("Queue<PlannedCraftJob> _outbox", StringComparison.Ordinal))
            && textByFile.Values.Any(static text => text.Contains("Queue<PlannedCraftJob> _backlog", StringComparison.Ordinal))
            && combined.Contains("_queue.Clear()", StringComparison.Ordinal)
            && combined.Contains("_backlog.Clear()", StringComparison.Ordinal),
            "Jobs backlog/planner queues should stay deterministic owner queues instead of ConcurrentQueue scheduling state.");
        Console.WriteLine("[PASS] Jobs backlog and planner queues use deterministic owner queues");
    }

    private static void TestTransportStatsAreExecutorOwned(string root)
    {
        string transportPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Transport");
        string statsFile = Path.Combine(transportPath, "TransportStats.cs");
        string trackerPath = Path.Combine(transportPath, "TransportStatsTracker.cs");
        string pickupPath = Path.Combine(transportPath, "TransportPickupHandler.cs");
        string deliveryPath = Path.Combine(transportPath, "TransportDeliveryHandler.cs");
        string executorPath = Path.Combine(transportPath, "TransportJobExecutor.cs");
        string readPath = Path.Combine(transportPath, "TransportJobExecutor.Read.cs");

        var activeText = string.Join('\n', TestRepositoryPaths
            .EnumerateSourceFiles(transportPath)
            .Select(File.ReadAllText));
        string trackerText = File.ReadAllText(trackerPath);
        string pickupText = File.ReadAllText(pickupPath);
        string deliveryText = File.ReadAllText(deliveryPath);
        string executorText = File.ReadAllText(executorPath);
        string readText = File.ReadAllText(readPath);

        RegressionAssert.True(
            !File.Exists(statsFile)
            && !activeText.Contains("JobStats.", StringComparison.Ordinal)
            && !activeText.Contains("static int Assigned", StringComparison.Ordinal)
            && !activeText.Contains("static int Completed", StringComparison.Ordinal)
            && !activeText.Contains("static int NoPath", StringComparison.Ordinal)
            && !activeText.Contains("static int Requeued", StringComparison.Ordinal)
            && trackerText.Contains("private int _completedTotal", StringComparison.Ordinal)
            && trackerText.Contains("private int _requeuedTotal", StringComparison.Ordinal)
            && trackerText.Contains("private int _noPathTotal", StringComparison.Ordinal)
            && pickupText.Contains("_stats.RecordNoPath()", StringComparison.Ordinal)
            && deliveryText.Contains("_stats.RecordCompleted()", StringComparison.Ordinal)
            && executorText.Contains("private readonly TransportStatsTracker _statsTracker = new()", StringComparison.Ordinal)
            && executorText.Contains("_statsTracker,", StringComparison.Ordinal)
            && readText.Contains("_statsTracker.RecordRequeued(", StringComparison.Ordinal),
            "Transport stats should be executor-owned state, not static JobStats counters shared across sessions.");
        Console.WriteLine("[PASS] Transport stats are executor-owned");
    }

    private static void TestProfessionSelectionUsesStableGuidTieBreaks(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Jobs", "Profession", "ProfessionAssignments.cs");
        string text = File.ReadAllText(path);
        string initializeBody = ExtractBetween(
            text,
            "internal void Initialize",
            "internal void RecordJobCompletion");
        string rosterBody = ExtractBetween(
            text,
            "internal IReadOnlyList<ProfessionRosterEntry> GetRosterSnapshot",
            "internal IEnumerable<CreatureInstance> SelectCandidates");
        string selectBody = ExtractBetween(
            text,
            "internal IEnumerable<CreatureInstance> SelectCandidates",
            "private int GetSkill");

        RegressionAssert.True(
            initializeBody.Contains("OrderBy(static creature => creature.Guid)", StringComparison.Ordinal)
            && rosterBody.Contains("OrderBy(static creature => creature.Guid)", StringComparison.Ordinal)
            && rosterBody.Contains("a.WorkerId.CompareTo(b.WorkerId)", StringComparison.Ordinal)
            && selectBody.Contains("ThenBy(pair => pair.Worker.Guid)", StringComparison.Ordinal),
            "Profession initialization, roster, and worker candidate selection need stable GUID tie-breaks instead of inheriting manager snapshot order.");
        Console.WriteLine("[PASS] Profession selection uses stable GUID tie-breaks");
    }

    private static void TestChunkTileReadsAreSynchronized(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Simulation", "World", "Chunk.cs");
        string text = File.ReadAllText(path);

        RegressionAssert.True(
            text.Contains("readers never observe a torn", StringComparison.Ordinal)
            && CountOccurrences(text, "lock (_writeLock)") >= 5,
            "Chunk tile reads/copies must stay synchronized with TileBase writes until a packed or frame-snapshot tile store replaces live struct-array reads.");
        Console.WriteLine("[PASS] Chunk tile reads are synchronized with writes");
    }

    private static void TestWorldGenerationSeedUsesExplicitEndianEncoding(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.App", "WorldGeneration", "WorldGenerationSettingsDefaults.cs");
        string text = File.ReadAllText(path);

        RegressionAssert.True(
            !text.Contains("BitConverter.ToUInt32", StringComparison.Ordinal)
            && text.Contains("BinaryPrimitives.ReadUInt32LittleEndian", StringComparison.Ordinal),
            "World generation random seed creation must use explicit endian encoding.");
        Console.WriteLine("[PASS] World generation seed uses explicit endian encoding");
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ExtractBetween(string text, string start, string end)
    {
        int startIndex = text.IndexOf(start, StringComparison.Ordinal);
        if (startIndex < 0)
            return string.Empty;

        int endIndex = text.IndexOf(end, startIndex, StringComparison.Ordinal);
        return endIndex < 0
            ? text[startIndex..]
            : text[startIndex..endIndex];
    }

    private static IEnumerable<string> EnumerateAuthorityFiles(string root)
    {
        string sourceRoot = Path.Combine(root, "src");
        foreach (var file in TestRepositoryPaths.EnumerateSourceFiles(sourceRoot))
        {
            string relative = TestRepositoryPaths.RelativePath(root, file);
            if (IsAuthorityPath(relative))
                yield return file;
        }
    }

    private static bool IsAuthorityPath(string relative)
    {
        if (relative.StartsWith("src/HumanFortress.Core/Commands/", StringComparison.Ordinal))
            return true;

        if (relative.StartsWith("src/HumanFortress.Core/Determinism/", StringComparison.Ordinal))
            return true;

        if (relative.StartsWith("src/HumanFortress.Runtime/Save/", StringComparison.Ordinal)
            || relative.StartsWith("src/HumanFortress.Runtime/Replay/", StringComparison.Ordinal))
            return true;

        if (relative.StartsWith("src/HumanFortress.Simulation/Save/", StringComparison.Ordinal)
            || relative.StartsWith("src/HumanFortress.Simulation/Replay/", StringComparison.Ordinal))
            return true;

        if (relative.StartsWith("src/HumanFortress.Jobs/Replay/", StringComparison.Ordinal))
            return true;

        if (!relative.StartsWith("src/HumanFortress.Runtime/", StringComparison.Ordinal))
            return false;

        string fileName = Path.GetFileName(relative);
        return fileName.Contains("Replay", StringComparison.Ordinal)
            || fileName.Contains("Save", StringComparison.Ordinal)
            || fileName.Contains("Checkpoint", StringComparison.Ordinal);
    }
}
