using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Identity;
using HumanFortress.Contracts.Content.Loading;
using HumanFortress.Runtime.Content;
using HumanFortress.Simulation.World;

internal static class StrictRuntimeContentGateRegressionTests
{
    internal static void RunAll()
    {
        TestStrictResultAndRuntimeReportEveryMechanicalGateFamily();
        TestLoadedCatalogIdsRequireCompiledHandles();
    }

    private static void TestStrictResultAndRuntimeReportEveryMechanicalGateFamily()
    {
        var root = CreateInvalidContentRoot();
        try
        {
            var first = FortressContentLoader.LoadStrictResult(root);
            var second = FortressContentLoader.LoadStrictResult(root);
            var report = first.ToReport();
            var firstSignature = IssueSignature(first.Issues);

            RegressionAssert.True(
                firstSignature == IssueSignature(second.Issues)
                && firstSignature == IssueSignature(report.Issues)
                && first.Issues.Any(static issue =>
                    issue.Code == "Content.Identity.DuplicateId")
                && first.Issues.Any(static issue =>
                    issue.Code == "Content.Reference.Missing")
                && first.Issues.Any(static issue =>
                    issue.Code == "Content.Schema.ValidationFailed"
                    && issue.Message.Contains(
                        "data/core/items/zz_strict_gate_schema.json",
                        StringComparison.Ordinal))
                && first.Issues.Any(static issue =>
                    issue.Code == "Content.GeneratedOutput.SourceMissing"),
                "Strict content result/report did not expose stable duplicate, reference, schema, and generated-freshness issues.");

            FortressContentLoadReport? strictReport = null;
            FortressContentLoadException? strictFailure = null;
            try
            {
                SimulationWorldContentLoader.LoadCoreContent(
                    new World(sizeInChunks: 2, maxZ: 3),
                    root,
                    strictContent: true,
                    logContentIssues: value => strictReport = value);
            }
            catch (FortressContentLoadException exception)
            {
                strictFailure = exception;
            }

            RegressionAssert.True(
                strictReport != null
                && strictFailure != null
                && IssueSignature(strictReport.GetBlockingIssues())
                    == IssueSignature(strictFailure.BlockingIssues)
                && strictFailure.BlockingIssues.Any(static issue =>
                    issue.Code == "Content.Identity.DuplicateId")
                && strictFailure.BlockingIssues.Any(static issue =>
                    issue.Code == "Content.Reference.Missing")
                && strictFailure.BlockingIssues.Any(static issue =>
                    issue.Code == "Content.Schema.ValidationFailed"
                    && issue.Message.Contains(
                        "data/core/items/zz_strict_gate_schema.json",
                        StringComparison.Ordinal))
                && strictFailure.BlockingIssues.Any(static issue =>
                    issue.Code == "Content.GeneratedOutput.SourceMissing"),
                "Strict Runtime composition did not publish its stable report before blocking world content application.");

            FortressContentLoadReport? compatibilityReport = null;
            var compatibilityWorld = new World(sizeInChunks: 2, maxZ: 3);
            _ = SimulationWorldContentLoader.LoadCoreContent(
                compatibilityWorld,
                root,
                strictContent: false,
                logContentIssues: value => compatibilityReport = value);
            RegressionAssert.True(
                compatibilityReport != null
                && compatibilityWorld.Items.DefinitionCount > 0
                && compatibilityWorld.Creatures.DefinitionCount > 0
                && !compatibilityReport.Issues.Any(static issue => IsStrictGateIssue(issue.Code)),
                "Non-strict Runtime composition stopped preserving the permissive compatibility path.");
        }
        finally
        {
            DeleteTree(root);
        }

        Console.WriteLine("[PASS] Strict Runtime content gate reports and blocks every mechanical issue family");
    }

    private static void TestLoadedCatalogIdsRequireCompiledHandles()
    {
        var loaded = FortressContentLoader.Load(AppContext.BaseDirectory);
        var catalogs = loaded.CoreCatalogs
            ?? throw new InvalidOperationException("Core catalogs were unavailable for handle binding evidence.");
        var identity = loaded.MechanicalIdentity
            ?? throw new InvalidOperationException("Mechanical identity was unavailable for handle binding evidence.");
        var loadedCatalogIds = GetLoadedCatalogIds(loaded, catalogs);
        var expectedNamespaces = new[]
        {
            "item",
            "creature",
            "construction",
            "recipe",
            "material",
            "terrain",
            "geology",
            "zone",
            "profession",
            "stockpile.preset",
        };
        RegressionAssert.True(
            expectedNamespaces.All(@namespace => loadedCatalogIds.Any(row => row.Namespace == @namespace))
            && loadedCatalogIds.All(row =>
                identity.TryGetLocalHandle(row.Namespace, row.CanonicalId, out _)),
            "Baseline mechanical identity did not bind every loaded production catalog id.");

        var removed = loadedCatalogIds.First(static row => row.Namespace == "item");
        var identityWithoutItem = new MechanicalContentIdentityData(
            identity.FormatId,
            identity.FormatVersion,
            identity.CosmeticPolicyId,
            identity.MechanicalSignature,
            identity.Sections,
            identity.LocalHandles
                .Where(handle => !(handle.Namespace == removed.Namespace
                                   && handle.CanonicalId == removed.CanonicalId))
                .ToArray(),
            identity.ExcludedSources,
            identity.Issues,
            identity.SchemaValidation);

        var issues = MechanicalContentCatalogIdentityValidator.Validate(
            loaded.StructuredRegistry,
            catalogs,
            loaded.Professions,
            loaded.StockpilePresetDefinitions,
            identityWithoutItem);
        RegressionAssert.True(
            issues.Any(issue =>
                issue.Code == "Content.Identity.CatalogHandleMissing"
                && issue.Source == "catalog/item"
                && issue.Message.Contains(
                    $"{removed.Namespace}:{removed.CanonicalId}",
                    StringComparison.Ordinal)),
            "A production item catalog id without its compiled local handle was not rejected.");

        var extraHandle = new MechanicalContentLocalHandleData(
            identity.LocalHandles.Max(static row => row.Handle) + 1,
            "item",
            "core_item_strict_gate_unloaded");
        var identityWithUnloadedItem = new MechanicalContentIdentityData(
            identity.FormatId,
            identity.FormatVersion,
            identity.CosmeticPolicyId,
            identity.MechanicalSignature,
            identity.Sections,
            identity.LocalHandles.Append(extraHandle).ToArray(),
            identity.ExcludedSources,
            identity.Issues,
            identity.SchemaValidation);
        var reverseIssues = MechanicalContentCatalogIdentityValidator.Validate(
            loaded.StructuredRegistry,
            catalogs,
            loaded.Professions,
            loaded.StockpilePresetDefinitions,
            identityWithUnloadedItem);
        RegressionAssert.True(
            reverseIssues.Any(issue =>
                issue.Code == "Content.Identity.CatalogDefinitionMissing"
                && issue.Source == "catalog/item"
                && issue.Message.Contains(extraHandle.QualifiedId, StringComparison.Ordinal)),
            "An active identity handle omitted by the Runtime catalog was not rejected.");

        Console.WriteLine("[PASS] Loaded production catalogs and canonical identity handles are bidirectionally bound");
    }

    private static IReadOnlyList<(string Namespace, string CanonicalId)> GetLoadedCatalogIds(
        FortressContentLoadResult loaded,
        HumanFortress.Content.Definitions.CoreContentCatalogLoadResult catalogs)
    {
        var rows = new List<(string Namespace, string CanonicalId)>();
        rows.AddRange(catalogs.Items.Catalog.GetAllDefinitions()
            .Select(static definition => ("item", definition.Id)));
        rows.AddRange(catalogs.Creatures.Catalog.GetAllDefinitions()
            .Select(static definition => ("creature", definition.Id)));
        rows.AddRange(catalogs.Constructions.Catalog.GetAllConstructions()
            .Select(static definition => ("construction", definition.Id)));
        rows.AddRange(catalogs.Recipes.Catalog.GetAllRecipes()
            .Select(static definition => ("recipe", definition.Id)));
        rows.AddRange(loaded.StructuredRegistry.GetLoadedMaterialCanonicalIds()
            .Select(static id => ("material", id)));
        rows.AddRange(loaded.StructuredRegistry.TerrainKinds.GetAllKinds()
            .Select(static definition => ("terrain", definition.Name)));
        rows.AddRange(loaded.StructuredRegistry.GeologyEntries.Keys
            .Select(static id => ("geology", id)));
        rows.AddRange(loaded.StructuredRegistry.Zones.Keys
            .Select(static id => ("zone", id)));
        if (loaded.Professions != null)
        {
            rows.AddRange(loaded.Professions.Definitions
                .Select(static definition => ("profession", definition.Id)));
        }
        rows.AddRange(loaded.StockpilePresetDefinitions
            .Select(static definition => ("stockpile.preset", definition.Id)));
        return rows
            .OrderBy(static row => row.Namespace, StringComparer.Ordinal)
            .ThenBy(static row => row.CanonicalId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string CreateInvalidContentRoot()
    {
        var repositoryRoot = TestRepositoryPaths.FindRepositoryRoot();
        var root = Path.Combine(
            Path.GetTempPath(),
            "HumanFortressStrictRuntimeGate",
            Guid.NewGuid().ToString("N"));
        CopyTree(Path.Combine(repositoryRoot, "content"), Path.Combine(root, "content"));
        CopyTree(Path.Combine(repositoryRoot, "data", "core"), Path.Combine(root, "data", "core"));

        File.Delete(Path.Combine(root, "content", "registries", "materials.authoring.json"));
        File.WriteAllText(
            Path.Combine(root, "data", "core", "items", "zz_strict_gate_duplicate.json"),
            """
            [
              {
                "id": "core_item_ingot_iron_wrought",
                "name": "Strict Gate Duplicate",
                "kind": "RESOURCE",
                "tags": ["strict_gate"],
                "base_volume_ml": 1
              }
            ]
            """);
        File.WriteAllText(
            Path.Combine(root, "data", "core", "items", "zz_strict_gate_schema.json"),
            """
            {
              "items": [
                {
                  "id": "core_item_strict_gate_schema",
                  "name": "Strict Gate Schema Failure",
                  "tags": ["strict_gate"],
                  "base_volume_ml": 1
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(root, "data", "core", "recipes", "core_strict_gate_reference.json"),
            """
            {
              "recipes": [
                {
                  "id": "core_recipe_strict_gate_missing_reference",
                  "inputs": [
                    { "def_id": "core_item_strict_gate_missing", "count": 1 }
                  ],
                  "outputs": []
                }
              ]
            }
            """);
        return root;
    }

    private static void CopyTree(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string IssueSignature(IEnumerable<FortressContentIssue> issues)
    {
        return string.Join(
            "\n",
            issues.Select(static issue =>
                $"{issue.Severity}|{issue.Code}|{issue.Message}"));
    }

    private static bool IsStrictGateIssue(string code)
    {
        return code.StartsWith("Content.Identity.", StringComparison.Ordinal)
            || code.StartsWith("Content.Reference.", StringComparison.Ordinal)
            || code.StartsWith("Content.Schema.", StringComparison.Ordinal)
            || code.StartsWith("Content.GeneratedOutput.", StringComparison.Ordinal);
    }

    private static void DeleteTree(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
