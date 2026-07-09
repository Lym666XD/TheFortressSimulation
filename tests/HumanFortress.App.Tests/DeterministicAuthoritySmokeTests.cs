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
        TestRuntimeInvalidatesPathCachesForDirtyNavigationChunks(root);
        TestTickSchedulerUsesDeterministicSystemOrder(root);
        TestDefaultSmokeRunnerExecutesFullRuntimeDeterminismGate(root);
        TestSanitizeSystemUsesTickDerivedSchedule(root);
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
        var forbiddenTokens = new[] { "Stopwatch", "ElapsedMilliseconds" };
        var files = new[]
        {
            Path.Combine(root, "src", "HumanFortress.Navigation", "DeterministicAStar.cs"),
            Path.Combine(root, "src", "HumanFortress.Navigation", "PathService.cs")
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

        RegressionAssert.True(
            violations.Count == 0,
            "Navigation pathfinding must use deterministic node/request budgets, not wall-clock time:\n"
            + string.Join('\n', violations));
        Console.WriteLine("[PASS] Navigation pathfinding avoids wall-clock budgets");
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

    private static void TestTickSchedulerUsesDeterministicSystemOrder(string root)
    {
        string schedulerPath = Path.Combine(root, "src", "HumanFortress.Core", "Time", "TickScheduler.cs");
        string hostLifecyclePath = Path.Combine(root, "src", "HumanFortress.Runtime", "Host", "SimulationRuntimeHost.Lifecycle.cs");
        string schedulerText = File.ReadAllText(schedulerPath);
        string hostLifecycleText = File.ReadAllText(hostLifecyclePath);

        RegressionAssert.True(
            schedulerText.Contains("_systems.Sort(CompareSystems)", StringComparison.Ordinal)
            && schedulerText.Contains("string.CompareOrdinal(a.SystemId, b.SystemId)", StringComparison.Ordinal)
            && !schedulerText.Contains("Parallel.ForEach(systems", StringComparison.Ordinal)
            && hostLifecycleText.Contains("AttachForManualTicks", StringComparison.Ordinal)
            && hostLifecycleText.Contains("_core.Configure", StringComparison.Ordinal),
            "TickScheduler and Runtime manual tick host must use deterministic system order and avoid read-phase parallelism for replay smoke tests.");
        Console.WriteLine("[PASS] TickScheduler uses deterministic system ordering");
    }

    private static void TestDefaultSmokeRunnerExecutesFullRuntimeDeterminismGate(string root)
    {
        string programPath = Path.Combine(root, "tests", "HumanFortress.App.Tests", "Program.cs");
        string coreRuntimeSmokePath = Path.Combine(root, "tests", "HumanFortress.App.Tests", "CoreRuntimeSmokeTests.cs");
        string programText = File.ReadAllText(programPath);
        string coreRuntimeSmokeText = File.ReadAllText(coreRuntimeSmokePath);
        string runAllBody = ExtractBetween(
            coreRuntimeSmokeText,
            "public static void RunAll()",
            "private static void TestTickScheduler()");

        RegressionAssert.True(
            programText.Contains("CoreRuntimeSmokeTests.RunAll()", StringComparison.Ordinal)
            && programText.Contains("DeterministicAuthoritySmokeTests.RunAll()", StringComparison.Ordinal)
            && runAllBody.Contains("TestFullRuntimeSimulationLoopDeterminism();", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("const int tickCount = 200;", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("var first = CreateManualRuntimeDeterminismRun();", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("var second = CreateManualRuntimeDeterminismRun();", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("RunManualRuntimeDeterminismLoop", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("firstSamples.SequenceEqual(secondSamples)", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("RuntimeReplayCheckpointHashBuilder.BuildData", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("WorldReplayHashBuilder.Build(run.Session.World)", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("checkpoint.RngHash", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("checkpoint.CommandLogHash", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("checkpoint.PendingCommandLogHash", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("sample.CommandLogRecordCount == 1", StringComparison.Ordinal)
            && coreRuntimeSmokeText.Contains("sample.PendingCommandLogRecordCount == 0", StringComparison.Ordinal),
            "Default smoke runner must execute the full Runtime simulation-loop determinism gate and compare world/checkpoint/RNG/command replay hashes.");
        Console.WriteLine("[PASS] Default smoke runner executes full Runtime determinism gate");
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

    private static void TestDiffTargetGuidEncodingIsPlatformStable(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Core", "Simulation", "DiffTargetEncoding.cs");
        string worldCellPath = Path.Combine(root, "src", "HumanFortress.Simulation", "World", "WorldCellTargetEncoding.cs");
        string text = File.ReadAllText(path);
        string worldCellText = File.ReadAllText(worldCellPath);

        RegressionAssert.True(
            !text.Contains("BitConverter.ToUInt32", StringComparison.Ordinal)
            && text.Contains("BinaryPrimitives.ReadUInt32LittleEndian", StringComparison.Ordinal)
            && text.Contains("BinaryPrimitives.ReadUInt64LittleEndian", StringComparison.Ordinal)
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
        string contractText = File.ReadAllText(contractPath);
        string implementationText = File.ReadAllText(implementationPath);

        RegressionAssert.True(
            contractText.Contains("ulong entityKey", StringComparison.Ordinal)
            && !contractText.Contains("uint entityId", StringComparison.Ordinal)
            && implementationText.Contains("Dictionary<ulong, MovementState>", StringComparison.Ordinal),
            "Movement execution state must be keyed by the wider stable entity key, not truncated uint entity ids.");
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
        string rngPath = Path.Combine(root, "src", "HumanFortress.Core", "Random", "RngStreamManager.cs");
        string materialPath = Path.Combine(root, "src", "HumanFortress.Content", "Registry", "MaterialRegistry.cs");
        string itemCatalogPath = Path.Combine(root, "src", "HumanFortress.Contracts", "Simulation", "Items", "ItemDefinitionCatalogStore.cs");
        string creatureCatalogPath = Path.Combine(root, "src", "HumanFortress.Contracts", "Simulation", "Creatures", "CreatureDefinitionCatalogStore.cs");
        string constructionCatalogPath = Path.Combine(root, "src", "HumanFortress.Contracts", "Content", "Registry", "ConstructionCatalogStore.cs");
        string recipeCatalogPath = Path.Combine(root, "src", "HumanFortress.Contracts", "Content", "Registry", "RecipeDefinition.cs");
        string constructionLoaderPath = Path.Combine(root, "src", "HumanFortress.Content", "Definitions", "CoreDataRegistryLoader.Constructions.cs");
        string runtimeSaveSnapshotPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotData.cs");
        string rngText = File.ReadAllText(rngPath);
        string materialText = File.ReadAllText(materialPath);
        string itemCatalogText = File.ReadAllText(itemCatalogPath);
        string creatureCatalogText = File.ReadAllText(creatureCatalogPath);
        string constructionCatalogText = File.ReadAllText(constructionCatalogPath);
        string recipeCatalogText = File.ReadAllText(recipeCatalogPath);
        string constructionLoaderText = File.ReadAllText(constructionLoaderPath);
        string runtimeSaveSnapshotText = File.ReadAllText(runtimeSaveSnapshotPath);

        RegressionAssert.True(
            CountOccurrences(rngText, "OrderBy(static kvp => kvp.Key, StringComparer.Ordinal)") >= 2
            && materialText.Contains("OrderBy(static material => material.StringId, StringComparer.Ordinal)", StringComparison.Ordinal)
            && materialText.Contains("OrderBy(static entry => entry.Key, StringComparer.Ordinal)", StringComparison.Ordinal)
            && itemCatalogText.Contains("OrderBy(static definition => definition.Id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && itemCatalogText.Contains("OrderBy(static id => id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && creatureCatalogText.Contains("OrderBy(static definition => definition.Id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && constructionCatalogText.Contains("OrderBy(static construction => construction.Id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && constructionCatalogText.Contains("OrderBy(static category => category, StringComparer.Ordinal)", StringComparison.Ordinal)
            && recipeCatalogText.Contains("OrderBy(static recipe => recipe.Id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && recipeCatalogText.Contains("string.Compare(a.Id, b.Id, StringComparison.Ordinal)", StringComparison.Ordinal)
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
        string restorePath = Path.Combine(root, "src", "HumanFortress.Simulation", "Items", "ItemManager.SaveRestore.cs");
        string applicatorPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Diff", "SimulationDiffApplicator.cs");
        string managerText = File.ReadAllText(managerPath);
        string indexingText = File.ReadAllText(indexingPath);
        string queriesText = File.ReadAllText(queriesPath);
        string mutationsText = File.ReadAllText(mutationsPath);
        string restoreText = File.ReadAllText(restorePath);
        string applicatorText = File.ReadAllText(applicatorPath);
        string entityKeyLookupBody = ExtractBetween(
            queriesText,
            "public ItemInstance? GetInstanceByEntityKey",
            "/// <summary>\n    /// Get all instances");
        string legacyLookupBody = ExtractBetween(
            queriesText,
            "public ItemInstance? GetInstanceByEntityId",
            "/// <summary>\n    /// Find an item by the wider stable entity key");
        string findItemBody = ExtractBetween(
            applicatorText,
            "private static ItemInstance? FindItemByTarget",
            "private static CreatureInstance? FindCreatureByTarget");

        RegressionAssert.True(
            managerText.Contains("Dictionary<ulong, Guid> _entityKeyIndex", StringComparison.Ordinal)
            && managerText.Contains("Dictionary<uint, List<Guid>> _legacyEntityIdIndex", StringComparison.Ordinal)
            && indexingText.Contains("EntityKeyIndexAdd", StringComparison.Ordinal)
            && indexingText.Contains("EntityKeyIndexRemove", StringComparison.Ordinal)
            && indexingText.Contains("LegacyEntityIdIndexAdd", StringComparison.Ordinal)
            && indexingText.Contains("list.Sort();", StringComparison.Ordinal)
            && queriesText.Contains("_entityKeyIndex.TryGetValue", StringComparison.Ordinal)
            && queriesText.Contains("_legacyEntityIdIndex.TryGetValue", StringComparison.Ordinal)
            && queriesText.Contains("OrderBy(static inst => inst.Guid)", StringComparison.Ordinal)
            && queriesText.Contains("OrderItemsSpatially", StringComparison.Ordinal)
            && queriesText.Contains("ThenBy(static item => item.Guid)", StringComparison.Ordinal)
            && mutationsText.Contains("foreach (var gid in ids.OrderBy(static id => id))", StringComparison.Ordinal)
            && mutationsText.Contains("list.Sort();", StringComparison.Ordinal)
            && mutationsText.Contains("byDef.OrderBy(static entry => entry.Key, StringComparer.Ordinal)", StringComparison.Ordinal)
            && restoreText.Contains("_entityKeyIndex.Clear()", StringComparison.Ordinal)
            && restoreText.Contains("_legacyEntityIdIndex.Clear()", StringComparison.Ordinal)
            && restoreText.Contains("EntityKeyIndexAdd(instance.Guid)", StringComparison.Ordinal)
            && !entityKeyLookupBody.Contains("foreach", StringComparison.Ordinal)
            && !legacyLookupBody.Contains("foreach", StringComparison.Ordinal)
            && findItemBody.Contains("GetInstanceByEntityKey(target.EntityKey)", StringComparison.Ordinal)
            && findItemBody.Contains("TryLegacyEntityId(target.EntityId", StringComparison.Ordinal)
            && !findItemBody.Contains("(uint)target.EntityId", StringComparison.Ordinal),
            "ItemManager entity lookup must stay indexed and maintained across restore; SimulationDiffApplicator must prefer entity keys and guard legacy fallback.");
        Console.WriteLine("[PASS] ItemManager entity lookup is indexed");
    }

    private static void TestCreatureManagerEntityKeyLookupIsIndexed(string root)
    {
        string managerPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Creatures", "CreatureManager.cs");
        string indexingPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Creatures", "CreatureManager.Indexing.cs");
        string queriesPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Creatures", "CreatureManager.Queries.cs");
        string restorePath = Path.Combine(root, "src", "HumanFortress.Simulation", "Creatures", "CreatureManager.SaveRestore.cs");
        string applicatorPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Diff", "SimulationDiffApplicator.cs");
        string managerText = File.ReadAllText(managerPath);
        string indexingText = File.ReadAllText(indexingPath);
        string queriesText = File.ReadAllText(queriesPath);
        string restoreText = File.ReadAllText(restorePath);
        string applicatorText = File.ReadAllText(applicatorPath);
        string entityKeyLookupBody = ExtractBetween(
            queriesText,
            "public CreatureInstance? GetInstanceByEntityKey",
            "/// <summary>\n    /// Get all instances");
        string legacyLookupBody = ExtractBetween(
            queriesText,
            "public CreatureInstance? GetInstanceByEntityId",
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
            managerText.Contains("Dictionary<ulong, Guid> _entityKeyIndex", StringComparison.Ordinal)
            && managerText.Contains("Dictionary<uint, List<Guid>> _legacyEntityIdIndex", StringComparison.Ordinal)
            && indexingText.Contains("EntityKeyIndexAdd", StringComparison.Ordinal)
            && indexingText.Contains("LegacyEntityIdIndexAdd", StringComparison.Ordinal)
            && queriesText.Contains("_entityKeyIndex.TryGetValue", StringComparison.Ordinal)
            && queriesText.Contains("_legacyEntityIdIndex.TryGetValue", StringComparison.Ordinal)
            && queriesText.Contains("OrderBy(static inst => inst.Guid)", StringComparison.Ordinal)
            && restoreText.Contains("_entityKeyIndex.Clear()", StringComparison.Ordinal)
            && restoreText.Contains("_legacyEntityIdIndex.Clear()", StringComparison.Ordinal)
            && restoreText.Contains("EntityKeyIndexAdd(instance.Guid)", StringComparison.Ordinal)
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
        string replayHashPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Replay", "WorldReplayHashBuilder.cs");
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
            zoneManagerText.Contains("OrderBy(static definition => definition.Id, StringComparer.Ordinal)", StringComparison.Ordinal)
            && CountOccurrences(zoneManagerText, "OrderBy(static zone => zone.ZoneId)") >= 2
            && zoneInstanceText.Contains("GetMemberChunksSnapshot", StringComparison.Ordinal)
            && chunkZoneDataText.Contains("OrderBy(static shard => shard.ZoneId)", StringComparison.Ordinal)
            && zoneCoordinatorText.Contains("zone.GetMemberChunksSnapshot()", StringComparison.Ordinal)
            && stockpileManagerText.Contains("OrderBy(static zone => zone.ZoneId)", StringComparison.Ordinal)
            && stockpileZoneText.Contains("GetMemberChunksSnapshot", StringComparison.Ordinal)
            && chunkStockpileDataText.Contains("OrderBy(static shard => shard.ZoneId)", StringComparison.Ordinal)
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
        string executorPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Transport", "TransportJobExecutor.cs");
        string runtimeBuilderPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "JobsDebugSnapshotBuilder.TransportDebug.cs");
        string text = File.ReadAllText(queuePath);
        string jobsDebugContractText = File.ReadAllText(jobsDebugContractPath);
        string jobsTransportDebugText = File.ReadAllText(jobsTransportDebugPath);
        string executorText = File.ReadAllText(executorPath);
        string runtimeBuilderText = File.ReadAllText(runtimeBuilderPath);

        RegressionAssert.True(
            text.Contains("_shards.OrderBy(static kv => kv.Key)", StringComparison.Ordinal)
            && text.Contains("_shards.Keys", StringComparison.Ordinal)
            && text.Contains("OrderBy(static shardId => shardId)", StringComparison.Ordinal)
            && jobsDebugContractText.Contains("TransportShardCountView", StringComparison.Ordinal)
            && jobsDebugContractText.Contains("IReadOnlyList<TransportShardCountView> ShardCounts", StringComparison.Ordinal)
            && !jobsDebugContractText.Contains("IReadOnlyDictionary<int, int> ShardCounts", StringComparison.Ordinal)
            && jobsTransportDebugText.Contains("TransportShardCountDebugView", StringComparison.Ordinal)
            && jobsTransportDebugText.Contains("IReadOnlyList<TransportShardCountDebugView> ShardCounts", StringComparison.Ordinal)
            && executorText.Contains("OrderBy(static kv => kv.Key)", StringComparison.Ordinal)
            && executorText.Contains("new TransportShardCountDebugView(kv.Key, kv.Value)", StringComparison.Ordinal)
            && runtimeBuilderText.Contains("new TransportShardCountView(shard.ShardId, shard.Count)", StringComparison.Ordinal)
            && !text.Contains("return _shards.Keys.ToArray();", StringComparison.Ordinal),
            "Transport queue and Runtime debug shard snapshots must expose shard ids/counts as stable shard-id ordered rows.");
        Console.WriteLine("[PASS] Transport queue shard snapshots use stable ordering");
    }

    private static void TestChunkLifecycleHeatDecayUsesStableOrdering(string root)
    {
        string path = Path.Combine(root, "src", "HumanFortress.Simulation", "World", "ChunkLifecycleManager.cs");
        string text = File.ReadAllText(path);

        RegressionAssert.True(
            text.Contains("OrderChunkKeys(_heatScores.Keys).ToArray()", StringComparison.Ordinal)
            && text.Contains("OrderBy(static key => key.Z)", StringComparison.Ordinal)
            && !text.Contains("_heatScores.Keys.ToList()", StringComparison.Ordinal),
            "Chunk lifecycle heat decay must iterate heat-score chunk keys in stable spatial order.");
        Console.WriteLine("[PASS] Chunk lifecycle heat decay uses stable ordering");
    }

    private static void TestWorldAndPlaceableSnapshotsUseStableOrdering(string root)
    {
        string worldPath = Path.Combine(root, "src", "HumanFortress.Simulation", "World", "World.cs");
        string chunkPlaceableDataPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Placeables", "ChunkPlaceableData.cs");
        string affectedChunksPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Placeables", "PlaceableManager.AffectedChunks.cs");
        string stockpileApplicatorPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Stockpile", "StockpileDiffApplicator.cs");
        string stockpileDetailPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "StockpileSnapshotBuilder.Detail.cs");
        string worldText = File.ReadAllText(worldPath);
        string chunkPlaceableDataText = File.ReadAllText(chunkPlaceableDataPath);
        string affectedChunksText = File.ReadAllText(affectedChunksPath);
        string stockpileApplicatorText = File.ReadAllText(stockpileApplicatorPath);
        string stockpileDetailText = File.ReadAllText(stockpileDetailPath);

        RegressionAssert.True(
            worldText.Contains("return OrderChunks(_chunks.Values).ToArray();", StringComparison.Ordinal)
            && worldText.Contains("return OrderChunks(_chunks.Values.Where", StringComparison.Ordinal)
            && worldText.Contains("OrderChunkKeys(_dirtyChunks).ToList()", StringComparison.Ordinal)
            && worldText.Contains("foreach (var chunk in GetAllChunks())", StringComparison.Ordinal)
            && chunkPlaceableDataText.Contains("GetOwnedPlaceableSnapshot()", StringComparison.Ordinal)
            && chunkPlaceableDataText.Contains("OrderBy(static entry => entry.Key)", StringComparison.Ordinal)
            && affectedChunksText.Contains("OrderBy(static chunk => chunk.Z)", StringComparison.Ordinal)
            && stockpileApplicatorText.Contains("acceptedCells\n            .OrderBy(static entry => entry.Key.Z)", StringComparison.Ordinal)
            && stockpileDetailText.Contains("TakeSorted(filter.Tags)", StringComparison.Ordinal)
            && stockpileDetailText.Contains("OrderBy(static value => value, StringComparer.Ordinal)", StringComparison.Ordinal)
            && !worldText.Contains("return _chunks.Values;", StringComparison.Ordinal)
            && !worldText.Contains("foreach (var chunk in _chunks.Values)", StringComparison.Ordinal)
            && !chunkPlaceableDataText.Contains("return _ownedPlaceables.Values;", StringComparison.Ordinal),
            "World/chunk/placeable owner snapshots must expose stable spatial/local-index ordering.");
        Console.WriteLine("[PASS] World and placeable snapshots use stable ordering");
    }

    private static void TestWorldSavePayloadRestoreUsesCanonicalOrdering(string root)
    {
        string restorerPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.cs");
        string validationPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.Validation.cs");
        string conversionPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.Conversion.cs");
        string placeablesPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadRestorer.Placeables.cs");
        string runtimeManifestBuilderPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveManifestBuilder.cs");
        string runtimeManifestSectionsPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveManifestSections.cs");
        string runtimeVerifierPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Save", "RuntimeSaveSnapshotDocumentVerifier.cs");
        string restorerText = File.ReadAllText(restorerPath);
        string validationText = File.ReadAllText(validationPath);
        string conversionText = File.ReadAllText(conversionPath);
        string placeablesText = File.ReadAllText(placeablesPath);
        string runtimeManifestBuilderText = File.ReadAllText(runtimeManifestBuilderPath);
        string runtimeManifestSectionsText = File.ReadAllText(runtimeManifestSectionsPath);
        string runtimeVerifierText = File.ReadAllText(runtimeVerifierPath);
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
            && validationText.Contains("ValidateStockpileZonePayloads", StringComparison.Ordinal)
            && validationText.Contains("ValidateOrderPayloads", StringComparison.Ordinal)
            && validationText.Contains("duplicates zone id", StringComparison.Ordinal)
            && validationText.Contains("duplicates mining id", StringComparison.Ordinal)
            && validationText.Contains("ValidateWorldRectangle", StringComparison.Ordinal)
            && validationText.Contains("ValidateWorldChunkKey", StringComparison.Ordinal)
            && conversionText.Contains("rows.OrderBy(static row => row.Key, StringComparer.Ordinal)", StringComparison.Ordinal)
            && conversionText.Contains("OrderBy(improvement => improvement.Type, StringComparer.Ordinal)", StringComparison.Ordinal)
            && conversionText.Contains("ThenBy(improvement => improvement.Description, StringComparer.Ordinal)", StringComparison.Ordinal)
            && placeablesText.Contains("ValidatePlaceablesSnapshot", StringComparison.Ordinal)
            && placeablesText.Contains("RestoreValidatedPlaceablesSnapshot", StringComparison.Ordinal)
            && placeablesText.Contains("OrderBy(placeable => placeable.Guid)", StringComparison.Ordinal)
            && placeablesText.Contains("ThenBy(placeable => placeable.OwnerLocalIndex)", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("ValidateManifestSections(document, issues)", StringComparison.Ordinal)
            && runtimeManifestSectionsText.Contains("internal static class RuntimeSaveManifestSections", StringComparison.Ordinal)
            && runtimeManifestSectionsText.Contains("RuntimeSaveManifestSectionDefinition", StringComparison.Ordinal)
            && runtimeManifestSectionsText.Contains("Array.AsReadOnly", StringComparison.Ordinal)
            && runtimeManifestSectionsText.Contains("internal static IEnumerable<string> OrderedNames", StringComparison.Ordinal)
            && runtimeManifestSectionsText.Contains("internal static bool TryGetRequirement", StringComparison.Ordinal)
            && runtimeManifestBuilderText.Contains("RuntimeSaveManifestSections.WorldTerrain", StringComparison.Ordinal)
            && runtimeManifestBuilderText.Contains("RuntimeSaveManifestSections.TryGetRequirement", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("RuntimeSaveManifestSections.TryGetRequirement", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("RuntimeSaveManifestSections.OrderedNames", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("new HashSet<string>(StringComparer.Ordinal)", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("Manifest section duplicates", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("is not recognized by this runtime", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("has a negative record count", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("is absent but still has a hash", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("is absent but still has a record count", StringComparison.Ordinal)
            && runtimeVerifierText.Contains("is missing", StringComparison.Ordinal),
            "World save payload restore and Runtime save document verification must use canonical owner order and strict manifest sections.");
        Console.WriteLine("[PASS] World save payload restore uses canonical ordering");
    }

    private static void TestConstructionMaterialSnapshotsUseStableOrdering(string root)
    {
        string constructionSitePath = Path.Combine(root, "src", "HumanFortress.Simulation", "Placeables", "ConstructionSiteState.cs");
        string trackerPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Construction", "ConstructionMaterialTracker.cs");
        string plannerPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Jobs", "ConstructionMaterialsPlanner.cs");
        string progressPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Construction", "ConstructionSiteProgress.cs");
        string coordinatorPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Construction", "ConstructionCompletionCoordinator.cs");
        string completionPath = Path.Combine(root, "src", "HumanFortress.Jobs", "Construction", "ConstructionCompletionApplier.cs");
        string workshopMaterialsPath = Path.Combine(root, "src", "HumanFortress.Runtime", "Snapshots", "WorkshopSnapshotBuilder.Materials.cs");
        string payloadBuilderPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Save", "WorldSavePayloadBuilder.Placeables.cs");
        string replayHashPath = Path.Combine(root, "src", "HumanFortress.Simulation", "Replay", "PlaceablesReplayHashBuilder.cs");
        string constructionSiteText = File.ReadAllText(constructionSitePath);
        string trackerText = File.ReadAllText(trackerPath);
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
            && constructionSiteText.Contains("StringComparer.OrdinalIgnoreCase", StringComparison.Ordinal)
            && trackerText.Contains("GetRequiredMaterialIdsSnapshot()", StringComparison.Ordinal)
            && trackerText.Contains("OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)", StringComparison.Ordinal)
            && plannerText.Contains("site.GetRequiredMaterialsSnapshot()", StringComparison.Ordinal)
            && plannerText.Contains("GetRequiredMaterialIdsSnapshot()", StringComparison.Ordinal)
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
            && !trackerText.Contains("MaterialsRequired.Keys", StringComparison.Ordinal)
            && !plannerText.Contains("MaterialsRequired.Keys", StringComparison.Ordinal)
            && !workshopMaterialsText.Contains("MaterialsRequired.Keys", StringComparison.Ordinal),
            "Construction material requirement/delivery readers must sort through ConstructionSiteState owner APIs.");
        Console.WriteLine("[PASS] Construction material snapshots use stable ordering");
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
