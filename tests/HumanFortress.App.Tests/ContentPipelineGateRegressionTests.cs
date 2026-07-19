using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HumanFortress.Content.Identity;
using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Identity;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Content;
using HumanFortress.Runtime.Host;
using HumanFortress.Runtime.Replay;
using HumanFortress.Runtime.Session;
using HumanFortress.Runtime.Startup;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;

internal static class ContentPipelineGateRegressionTests
{
    private static readonly MechanicalContentCanonicalPolicy PermutationPolicy =
        MechanicalContentCanonicalPolicy.Resolve("synthetic");

    internal static void RunAll()
    {
        Console.WriteLine("=== Content Pipeline Gate Regression Tests ===");
        ContentSourceFamilyManifestRegressionTests.RunAll();
        ContentMechanicalIdentityRegressionTests.RunAll();
        TestGeneratedOutputFreshnessIsDeterministicAndFailsClosedWhenActive();
        TestSemanticReferenceDuplicateAndCollisionIssuesAreStable();
        StrictRuntimeContentGateRegressionTests.RunAll();
        TestContentPermutationPreservesCatalogsAndCommittedRuntimeHash();
        TestRepositoryPassesStrictContentPipelineGate();
        Console.WriteLine("=== Content Pipeline Gate Regression Tests Completed ===");
        Console.WriteLine();
    }

    private static void TestGeneratedOutputFreshnessIsDeterministicAndFailsClosedWhenActive()
    {
        var root = CreateTemporaryRoot();
        var registries = Path.Combine(root, "content", "registries");
        Directory.CreateDirectory(registries);
        var sourcePath = Path.Combine(registries, "materials.authoring.json");
        var outputPath = Path.Combine(registries, "materials.registry.json");

        try
        {
            File.WriteAllText(sourcePath, """
                [
                  {
                    "id": "core_mat_test",
                    "aliases": ["test_material"],
                    "tags": ["metal", "test"],
                    "density_solid": 7800,
                    "hardness_edge": 50,
                    "toughness_frac": 60,
                    "rigidity": 70,
                    "electric_category": "conductor",
                    "mana_conductivity": 25,
                    "value_mul": 1.5,
                    "beauty_mul": 0.5,
                    "work": {
                      "forgeable": true,
                      "weldable": true,
                      "carveable": false,
                      "process_difficulty_mul": 1.25
                    }
                  }
                ]
                """);
            File.WriteAllText(outputPath, """
                [
                  {
                    "id": "core_mat_test",
                    "aliases": ["test_material"],
                    "tags": ["test", "metal"],
                    "density_solid": 7800,
                    "mechanics": {
                      "hardnessEdgeFx": 5000,
                      "toughnessFracFx": 6000,
                      "rigidityFx": 7000
                    },
                    "electricMagic": {
                      "manaConductivityFx": 2500,
                      "electricCategory": "conductor"
                    },
                    "economy": {
                      "valueMulFx": 15000,
                      "beautyMulFx": 5000
                    },
                    "work": {
                      "forgeable": true,
                      "weldable": true,
                      "carveable": false,
                      "processDifficultyMulFx": 12500
                    }
                  }
                ]
                """);

            var fresh = GeneratedContentFreshnessChecker.EvaluateMaterials(
                Path.Combine(root, "content"));
            var repeat = GeneratedContentFreshnessChecker.EvaluateMaterials(
                Path.Combine(root, "content"));
            RegressionAssert.True(
                fresh == repeat
                && fresh.Activation == GeneratedContentActivation.Inactive
                && fresh.Freshness == GeneratedContentFreshness.Fresh
                && fresh.SourceSemanticHash == fresh.OutputSemanticHash
                && !fresh.GeneratorAvailable
                && !fresh.IsBlocking,
                "Equivalent generated material semantics were not classified deterministically as inactive/fresh.");

            File.WriteAllText(
                outputPath,
                File.ReadAllText(outputPath).Replace("\"rigidityFx\": 7000", "\"rigidityFx\": 6900", StringComparison.Ordinal));
            var stale = GeneratedContentFreshnessChecker.EvaluateMaterials(
                Path.Combine(root, "content"));
            RegressionAssert.True(
                stale.Activation == GeneratedContentActivation.Inactive
                && stale.Freshness == GeneratedContentFreshness.Stale
                && stale.SourceSemanticHash != stale.OutputSemanticHash
                && !stale.IsBlocking,
                "A stale but shadowed legacy material output was treated as active or fresh.");

            File.Delete(sourcePath);
            var activeUnverifiable = GeneratedContentFreshnessChecker.EvaluateMaterials(
                Path.Combine(root, "content"));
            var blockingGate = MechanicalContentPipelineGate.Evaluate(
                new[]
                {
                    new MechanicalContentSourceDocument(
                        "core.items",
                        "items.json",
                        "item",
                        """[{"id":"core_item_test"}]""")
                },
                schemas: null,
                generatedOutputs: new[] { activeUnverifiable });
            RegressionAssert.True(
                activeUnverifiable.Activation == GeneratedContentActivation.Active
                && activeUnverifiable.Freshness == GeneratedContentFreshness.SourceMissing
                && activeUnverifiable.IsBlocking
                && !blockingGate.IsValid
                && blockingGate.Issues.Any(static issue =>
                    issue.Code == "Content.GeneratedOutput.SourceMissing"),
                "An active generated output without a reproducible source did not fail closed.");

            var repository = GeneratedContentFreshnessChecker.EvaluateMaterials(
                Path.Combine(TestRepositoryPaths.FindRepositoryRoot(), "content"));
            RegressionAssert.True(
                repository.Activation == GeneratedContentActivation.Inactive
                && repository.Freshness is GeneratedContentFreshness.Fresh or GeneratedContentFreshness.Stale
                && !repository.GeneratorAvailable
                && !repository.IsBlocking,
                "The checked-in legacy material registry was not explicitly classified as an inactive artifact.");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }

        Console.WriteLine("[PASS] Generated-output freshness is semantic, deterministic, and fail-closed when active");
    }

    private static void TestSemanticReferenceDuplicateAndCollisionIssuesAreStable()
    {
        var sources = new[]
        {
            new MechanicalContentSourceDocument(
                "registry.materials",
                "materials.json",
                "material",
                """[{"id":"core_shared","tags":["test"],"density_solid":1}]"""),
            new MechanicalContentSourceDocument(
                "core.items",
                "items-a.json",
                "item",
                """[{"id":"core_shared"},{"id":"core_item_dup"},{"id":"core_item_case"}]"""),
            new MechanicalContentSourceDocument(
                "core.items",
                "items-b.json",
                "item",
                """[{"id":"core_item_dup"},{"id":"core_item_Case"}]"""),
            new MechanicalContentSourceDocument(
                "core.recipes",
                "recipes.json",
                "recipe",
                """{"recipes":[{"id":"core_recipe_missing","inputs":[{"def_id":"core_item_missing","count":1}],"outputs":[]}]}""")
        };

        var first = MechanicalContentPipelineGate.Evaluate(sources, schemas: null);
        var second = MechanicalContentPipelineGate.Evaluate(sources.Reverse(), schemas: null);
        var sharedHandles = first.Identity.LocalHandles
            .Where(static handle => handle.CanonicalId == "core_shared")
            .OrderBy(static handle => handle.Namespace, StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            !first.IsValid
            && first.Issues.SequenceEqual(second.Issues)
            && first.Issues.Any(static issue => issue.Code == "Content.Identity.DuplicateId")
            && first.Issues.Any(static issue => issue.Code == "Content.Identity.AmbiguousId")
            && first.Issues.Any(static issue => issue.Code == "Content.Reference.Missing")
            && sharedHandles.Length == 2
            && sharedHandles[0].Namespace != sharedHandles[1].Namespace
            && sharedHandles[0].Handle != sharedHandles[1].Handle
            && first.Identity.LocalHandles.Select(static handle => handle.Handle).Distinct().Count()
                == first.Identity.LocalHandles.Count,
            "Semantic-reference, duplicate, ambiguity, or namespace collision diagnostics were unstable or permissive.");

        Console.WriteLine("[PASS] Semantic-reference and identity collision gates are stable and namespace-aware");
    }

    private static void TestContentPermutationPreservesCatalogsAndCommittedRuntimeHash()
    {
        var repositoryRoot = TestRepositoryPaths.FindRepositoryRoot();
        var baselineRoot = CreateTemporaryRoot();
        var permutedRoot = CreateTemporaryRoot();

        try
        {
            CopyContentTree(repositoryRoot, baselineRoot);
            CopyContentTree(repositoryRoot, permutedRoot);
            PermuteRepresentativeContent(permutedRoot);

            var baselineGate = MechanicalContentPipelineGate.Evaluate(baselineRoot);
            var permutedGate = MechanicalContentPipelineGate.Evaluate(permutedRoot);
            var baseline = FortressContentLoader.Load(baselineRoot);
            var permuted = FortressContentLoader.Load(permutedRoot);
            var baselineCatalog = BuildCatalogProjection(baseline);
            var permutedCatalog = BuildCatalogProjection(permuted);
            var baselineConstructionIds = baseline.CoreCatalogs!.Constructions.Catalog
                .GetAllConstructions()
                .Select(static definition => definition.Id)
                .ToHashSet(StringComparer.Ordinal);
            var permutedConstructionIds = permuted.CoreCatalogs!.Constructions.Catalog
                .GetAllConstructions()
                .Select(static definition => definition.Id)
                .ToHashSet(StringComparer.Ordinal);
            var baselineIdentity = baseline.MechanicalIdentity;
            var permutedIdentity = permuted.MechanicalIdentity;
            var baselineRuntimeHash = RunCommittedRuntimeFixture(baselineRoot);
            var permutedRuntimeHash = RunCommittedRuntimeFixture(permutedRoot);

            RegressionAssert.True(
                baselineGate.IsValid
                && permutedGate.IsValid
                && baselineGate.Identity.MechanicalSignature
                    == permutedGate.Identity.MechanicalSignature
                && baselineGate.Identity.LocalHandles.SequenceEqual(
                    permutedGate.Identity.LocalHandles)
                && baselineIdentity != null
                && permutedIdentity != null
                && baselineIdentity.MechanicalSignature == permutedIdentity.MechanicalSignature
                && baselineIdentity.Sections.SequenceEqual(permutedIdentity.Sections)
                && baselineIdentity.LocalHandles.SequenceEqual(permutedIdentity.LocalHandles)
                && baselineCatalog == permutedCatalog
                && baselineRuntimeHash == permutedRuntimeHash,
                "Content file/property/insertion permutation changed compiled catalogs, identity, or the production Runtime committed hash.\n"
                + $"gates={baselineGate.IsValid}/{permutedGate.IsValid}\n"
                + $"gate-signatures={baselineGate.Identity.MechanicalSignature}/{permutedGate.Identity.MechanicalSignature}\n"
                + $"gate-handles={baselineGate.Identity.LocalHandles.SequenceEqual(permutedGate.Identity.LocalHandles)}\n"
                + $"load-signatures={baselineIdentity?.MechanicalSignature}/{permutedIdentity?.MechanicalSignature}\n"
                + $"load-sections={baselineIdentity?.Sections.SequenceEqual(permutedIdentity?.Sections ?? Array.Empty<MechanicalContentSectionIdentityData>())}\n"
                + $"load-handles={baselineIdentity?.LocalHandles.SequenceEqual(permutedIdentity?.LocalHandles ?? Array.Empty<MechanicalContentLocalHandleData>())}\n"
                + $"catalogs={baselineCatalog}/{permutedCatalog}\n"
                + $"baseline-only-constructions={string.Join(',', baselineConstructionIds.Except(permutedConstructionIds).Order(StringComparer.Ordinal))}\n"
                + $"permuted-only-constructions={string.Join(',', permutedConstructionIds.Except(baselineConstructionIds).Order(StringComparer.Ordinal))}\n"
                + $"permuted-construction-messages={string.Join(" | ", permuted.CoreCatalogs.Constructions.Messages)}\n"
                + $"runtime-hashes={baselineRuntimeHash}/{permutedRuntimeHash}");
        }
        finally
        {
            DeleteTemporaryRoot(baselineRoot);
            DeleteTemporaryRoot(permutedRoot);
        }

        Console.WriteLine("[PASS] Permuted content preserves compiled catalogs and production Runtime committed hash");
    }

    private static void PermuteRepresentativeContent(string root)
    {
        var paths = new[]
        {
            "content/registries/creatures.body_plans.json",
            "content/registries/creatures.json",
            "content/registries/geology.json",
            "content/registries/materials.authoring.json",
            "content/registries/professions.json",
            "content/registries/stockpile_presets.json",
            "content/registries/terrain_kinds.json",
            "content/registries/tuning.mining.json",
            "content/registries/zones.json",
            "data/core/creatures/core_races.json",
            "data/core/items/metals.json",
            "data/core/placeable/constructions.json",
            "data/core/recipes/core_recipes.json",
            "data/core/workshops/core_workshop_kitchen.json",
        };
        foreach (var relativePath in paths)
            PermuteJsonFile(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        RenameWithinFamily(root, "data/core/creatures/core_races.json", "zzz_core_races_permuted.json");
        RenameWithinFamily(root, "data/core/items/metals.json", "zzz_metals_permuted.json");
        RenameWithinFamily(root, "data/core/recipes/core_recipes.json", "core_recipes_permuted.json");
        RenameWithinFamily(root, "data/core/workshops/core_workshop_kitchen.json", "core_workshop_kitchen_permuted.json");
    }

    private static void RenameWithinFamily(
        string root,
        string relativePath,
        string targetFileName)
    {
        var source = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Move(source, Path.Combine(Path.GetDirectoryName(source)!, targetFileName));
    }

    private static void TestRepositoryPassesStrictContentPipelineGate()
    {
        var repositoryRoot = TestRepositoryPaths.FindRepositoryRoot();
        var gate = MechanicalContentPipelineGate.Evaluate(repositoryRoot);
        var repeat = MechanicalContentPipelineGate.Evaluate(repositoryRoot);
        RegressionAssert.True(
            gate.IsValid
            && repeat.IsValid
            && gate.Identity.MechanicalSignature == repeat.Identity.MechanicalSignature
            && gate.Identity.Sections.SequenceEqual(repeat.Identity.Sections)
            && gate.Identity.LocalHandles.SequenceEqual(repeat.Identity.LocalHandles)
            && gate.Issues.SequenceEqual(repeat.Issues),
            "Repository content pipeline gate failed:\n"
            + string.Join("\n", gate.Issues.Select(static issue =>
                $"{issue.Severity} {issue.Code} {issue.Source}{issue.Path}: {issue.Message}")));

        RegressionAssert.True(
            gate.Identity.SchemaValidation.Availability == ContentSchemaValidatorAvailability.Available
            && gate.Identity.SchemaValidation.IsValid
            && gate.GeneratedOutputs.All(static output => !output.IsBlocking),
            "Strict content gate did not use the pinned schema adapter or left an active generated output unverified.");

        Console.WriteLine("[PASS] Repository passes strict schema, reference, identity, and generated-output gates");
    }

    private static CatalogProjection BuildCatalogProjection(FortressContentLoadResult load)
    {
        var catalogs = load.CoreCatalogs
            ?? throw new InvalidOperationException("Core catalogs were not loaded for content permutation evidence.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AddCatalog(hash, "items", catalogs.Items.Catalog.GetAllDefinitions().Cast<object>());
        AddCatalog(hash, "creatures", catalogs.Creatures.Catalog.GetAllDefinitions().Cast<object>());
        AddCatalog(hash, "constructions", catalogs.Constructions.Catalog.GetAllConstructions().Cast<object>());
        AddCatalog(hash, "recipes", catalogs.Recipes.Catalog.GetAllRecipes().Cast<object>());
        AddCatalog(hash, "terrain", load.StructuredRegistry.TerrainKinds.GetAllKinds().Cast<object>());
        AddCatalog(hash, "geology", load.StructuredRegistry.GeologyEntries.Values.Cast<object>());
        AddCatalog(hash, "zones", load.StructuredRegistry.Zones.Values.Cast<object>());

        var materialHandles = load.StructuredRegistry.Materials.GetNameToIdSnapshot()
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .Select(static entry => (object)new { Id = entry.Key, Handle = entry.Value });
        AddCatalog(hash, "material-handles", materialHandles);

        return new CatalogProjection(
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            load.StructuredRegistry.ContentHash,
            load.StructuredRegistry.Materials.ContentHash,
            catalogs.Items.Catalog.DefinitionCount,
            catalogs.Creatures.Catalog.DefinitionCount,
            catalogs.Constructions.Catalog.Count,
            catalogs.Recipes.Catalog.Count);
    }

    private static void AddCatalog(
        IncrementalHash hash,
        string catalogId,
        IEnumerable<object> definitions)
    {
        AddString(hash, catalogId);
        var canonical = definitions
            .Select(definition =>
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(definition, definition.GetType());
                using var document = JsonDocument.Parse(bytes);
                return CanonicalMechanicalJsonSerializer.Serialize(document.RootElement);
            })
            .OrderBy(static bytes => Convert.ToHexString(bytes), StringComparer.Ordinal)
            .ToArray();
        AddInt32(hash, canonical.Length);
        foreach (var bytes in canonical)
        {
            AddInt32(hash, bytes.Length);
            hash.AppendData(bytes);
        }
    }

    private static string RunCommittedRuntimeFixture(string root)
    {
        var services = new RuntimeSessionServices();
        FortressRuntimeContentSnapshot? content = null;
        var factory = new SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>>(
            services,
            world =>
            {
                content = SimulationWorldContentLoader.LoadCoreContent(world, root);
                FillRuntimeFixtureTerrain(world);
            },
            (world, navigation) => FortressRuntimeHostFactory.Create(
                world,
                services,
                navigation,
                root,
                content,
                FortressRuntimeLogging.None));
        var session = factory.CreateNew(sizeInChunks: 2, maxZ: 3);
        session.Navigation.RebuildAll();
        var systems = session.Host.AttachForManualTicks(configured =>
        {
            var spawned = SimulationInitialWorkerSpawner.SpawnIfNeeded(session.World, desired: 2);
            configured.ProfessionAssignments.Initialize(session.World.Creatures.GetAllInstances());
            RuntimeAutoDigSeeder.EnqueueIfPossible(
                session.World,
                services.CommandQueue,
                services.TickScheduler.CurrentTick);
            var item = session.World.Items.SpawnItem(
                "core_item_ingot_iron_wrought",
                new SadRogue.Primitives.Point(2, 2),
                z: 1,
                quantity: 2,
                currentTick: 0);
            RegressionAssert.True(
                spawned == 2 && item.HasValue,
                "Content permutation Runtime fixture failed to compose workers or an item from loaded catalogs.");
        });

        for (var tick = 0; tick < 4; tick++)
            services.TickScheduler.ExecuteSingleTick();

        var sessionHandle = new FortressRuntimeSession(session);
        var replay = RuntimeReplayCheckpointHashBuilder.BuildData(services, sessionHandle);
        var committed = RuntimeCommittedReplayHashBuilder.Build(replay, systems);
        _ = session.Host.Stop();
        return committed.Replay.AggregateHash;
    }

    private static void FillRuntimeFixtureTerrain(World world)
    {
        var floor = new TileBase(
            geoMatId: 0,
            terrainBits: (ushort)TerrainKind.OpenWithFloor,
            surfaceBits: 0,
            fluidKind: 0,
            fluidDepth: 0,
            metaBits: 0,
            trafficCost: 1);
        var wall = new TileBase(
            geoMatId: 0,
            terrainBits: (ushort)TerrainKind.SolidWall,
            surfaceBits: 0,
            fluidKind: 0,
            fluidDepth: 0,
            metaBits: 0,
            trafficCost: 1);

        for (var z = 0; z < world.MaxZ; z++)
        {
            for (var y = 0; y < world.SizeInTiles; y++)
            {
                for (var x = 0; x < world.SizeInTiles; x++)
                    world.SetTile(x, y, z, floor, tick: 0);
            }
        }

        world.SetTile(
            world.SizeInTiles / 2 + 4,
            world.SizeInTiles / 2,
            1,
            wall,
            tick: 0);
    }

    private static void PermuteJsonFile(string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Could not parse permutation fixture '{path}'.");
        var permuted = PermuteNode(root, propertyName: null, isRoot: true);
        File.WriteAllText(
            path,
            permuted.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static JsonNode PermuteNode(JsonNode node, string? propertyName, bool isRoot = false)
    {
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var property in obj.Reverse())
            {
                result[property.Key] = property.Value == null
                    ? null
                    : PermuteNode(property.Value, property.Key);
            }
            return result;
        }

        if (node is JsonArray array)
        {
            var values = array
                .Select(value => value == null ? null : PermuteNode(value, propertyName))
                .ToArray();
            if (isRoot || (propertyName != null && PermutationPolicy.IsUnorderedArray(propertyName)))
                Array.Reverse(values);

            var result = new JsonArray();
            foreach (var value in values)
                result.Add(value);
            return result;
        }

        return node.DeepClone();
    }

    private static void CopyContentTree(string repositoryRoot, string destinationRoot)
    {
        CopyDirectory(Path.Combine(repositoryRoot, "content"), Path.Combine(destinationRoot, "content"));
        CopyDirectory(Path.Combine(repositoryRoot, "data", "core"), Path.Combine(destinationRoot, "data", "core"));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string CreateTemporaryRoot()
    {
        return Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            "humanfortress-content-gate-" + Guid.NewGuid().ToString("N"))).FullName;
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private static void AddString(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AddInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AddInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private sealed record CatalogProjection(
        string CatalogHash,
        string StructuredContentHash,
        string MaterialContentHash,
        int ItemCount,
        int CreatureCount,
        int ConstructionCount,
        int RecipeCount);
}
