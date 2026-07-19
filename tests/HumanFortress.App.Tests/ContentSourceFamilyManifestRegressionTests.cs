using HumanFortress.Content.Identity;
using HumanFortress.Contracts.Content.Identity;

internal static class ContentSourceFamilyManifestRegressionTests
{
    internal static void RunAll()
    {
        Console.WriteLine("=== Content Source Family Manifest Regression Tests ===");
        TestManifestIsCompleteAndSelfConsistent();
        TestRepositorySourcesAreClassifiedAndSchemaBound();
        TestUnclassifiedScannedSourceFailsClosedDeterministically();
        TestManifestSchemaBindingOverridesDocumentUriSpelling();
        TestFamilyShapeAndRequiredPropertyIssuesAreStable();
        Console.WriteLine("=== Content Source Family Manifest Regression Tests Completed ===");
        Console.WriteLine();
    }

    private static void TestManifestIsCompleteAndSelfConsistent()
    {
        var declarations = MechanicalContentSourceFamilyManifest.Declarations;
        var orderedIds = declarations
            .Select(static declaration => declaration.FamilyId)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            MechanicalContentSourceFamilyManifest.CanonicalPolicyId
                == "humanfortress.content-source-families.v2"
            && declarations.Count > 0
            && declarations.Select(static declaration => declaration.FamilyId)
                .SequenceEqual(orderedIds)
            && orderedIds.Distinct(StringComparer.Ordinal).Count() == orderedIds.Length
            && declarations.All(static declaration =>
                declaration.ActivationPolicy == ContentSourceFamilyActivationPolicy.Excluded
                    ? declaration.SchemaId == null
                      && !string.IsNullOrWhiteSpace(declaration.ExclusionReason)
                    : !string.IsNullOrWhiteSpace(declaration.SchemaId))
            && declarations
                .Where(static declaration => declaration.ActivationPolicy is
                    ContentSourceFamilyActivationPolicy.ExcludedWhenAnySupersedingSourceExists
                    or ContentSourceFamilyActivationPolicy.ExcludedWhenAllSupersedingSourcesExist)
                .All(static declaration =>
                    !string.IsNullOrWhiteSpace(declaration.ExclusionReason)
                    && declaration.SupersedingSourcePatterns?.Count > 0),
            "Content source manifest identifiers, schemas, or exclusion contracts are incomplete or unstable.");

        Console.WriteLine("[PASS] Content source family manifest is ordered and self-consistent");
    }

    private static void TestRepositorySourcesAreClassifiedAndSchemaBound()
    {
        var root = TestRepositoryPaths.FindRepositoryRoot();
        var sourceSet = MechanicalContentSourceScanner.Scan(
            Path.Combine(root, "content"),
            Path.Combine(root, "data", "core"));
        var schemaIds = sourceSet.Schemas
            .Select(static schema => Path.GetFileName(schema.SourceId))
            .ToHashSet(StringComparer.Ordinal);
        var excluded = sourceSet.MechanicalSources
            .Where(static source => source.IsExcludedFromMechanicalIdentity)
            .Select(static source => source.SourceId)
            .OrderBy(static source => source, StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            sourceSet.MechanicalSources.Count > 0
            && sourceSet.MechanicalSources.All(static source =>
                source.RequiresSourceFamilyManifest
                && source.FamilyId != "unclassified")
            && sourceSet.MechanicalSources
                .Where(static source => !source.IsExcludedFromMechanicalIdentity)
                .All(source =>
                    !string.IsNullOrWhiteSpace(source.SchemaId)
                    && schemaIds.Contains(source.SchemaId))
            && excluded.SequenceEqual(new[]
            {
                "content/registries/creatures.body_plans.json",
                "content/registries/creatures.json",
                "content/registries/geology_prototypes.json",
                "content/registries/input.bindings.json",
                "content/registries/materials.registry.json",
                "content/registries/orders.registry.json",
                "content/registries/stockpiles.json",
                "content/registries/tuning.damage.json",
                "content/registries/tuning.hauling.json",
                "content/registries/tuning.stockpile.json",
                "content/registries/tuning.tile.json",
                "content/registries/ui.workshop_categories.json",
                "data/core/items/weapons.json",
                "data/core/placeable/terrain_designations.json",
                "data/core/placeable/workshop_attachments.json",
                "data/core/placeable/workshops.json",
                "data/core/recipes/recipes.fuel_alkali.json",
                "data/core/recipes/recipes.general.json",
                "data/core/recipes/recipes.glassblowing.json",
                "data/core/recipes/recipes.metallurgy.json",
                "data/core/recipes/recipes.pottery.json",
                "data/core/recipes/recipes.stoneworks.json",
                "data/core/recipes/recipes.woodworking.json",
            }),
            "A repository JSON source is unclassified, lacks a checked-in schema, or has unstable activation policy.");

        Console.WriteLine("[PASS] Repository content sources are classified, schema-bound, and explicitly activated");
    }

    private static void TestUnclassifiedScannedSourceFailsClosedDeterministically()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hf-content-family-" + Guid.NewGuid().ToString("N"));
        var registries = Path.Combine(root, "content", "registries");
        var core = Path.Combine(root, "data", "core");
        Directory.CreateDirectory(registries);
        Directory.CreateDirectory(core);

        try
        {
            File.WriteAllText(
                Path.Combine(registries, "unknown.gameplay.json"),
                """{"id":"unknown_definition"}""");
            var sourceSet = MechanicalContentSourceScanner.Scan(
                Path.Combine(root, "content"),
                core);
            var first = MechanicalContentIdentityCompiler.Compile(
                sourceSet.MechanicalSources,
                sourceSet.Schemas,
                JsonSchemaNetContentValidationAdapter.Instance,
                requireSchemaValidation: true);
            var second = MechanicalContentIdentityCompiler.Compile(
                sourceSet.MechanicalSources.Reverse(),
                sourceSet.Schemas.Reverse(),
                JsonSchemaNetContentValidationAdapter.Instance,
                requireSchemaValidation: true);

            RegressionAssert.True(
                sourceSet.MechanicalSources.Single().FamilyId == "unclassified"
                && first.SchemaValidation.Issues.SequenceEqual(second.SchemaValidation.Issues)
                && first.SchemaValidation.Issues.Count(static issue =>
                    issue.Code == "Content.Schema.SourceUnclassified"
                    && issue.Source == "content/registries/unknown.gameplay.json") == 1,
                "An unclassified scanned source did not fail closed with deterministic diagnostics.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        Console.WriteLine("[PASS] Unclassified active sources fail closed deterministically");
    }

    private static void TestManifestSchemaBindingOverridesDocumentUriSpelling()
    {
        var source = new MechanicalContentSourceDocument(
            "core.items",
            "data/core/items/synthetic.json",
            "item",
            """
            {
              "$schema":"https://outside.invalid/not-the-family-contract.json",
              "version":1,
              "items":[{"id":"core_item_synthetic"}]
            }
            """,
            FamilyId: "core.item",
            SchemaId: "source.item-catalog.schema.json",
            RequiresSourceFamilyManifest: true);
        var schema = new ContentSchemaSourceDocument(
            "content/schemas/source.item-catalog.schema.json",
            """
            {
              "$schema":"http://json-schema.org/draft-07/schema#",
              "$id":"https://humanfortress.game/schemas/source.item-catalog.schema.json",
              "type":"object",
              "required":["version","items"]
            }
            """);
        var identity = MechanicalContentIdentityCompiler.Compile(
            new[] { source },
            new[] { schema },
            JsonSchemaNetContentValidationAdapter.Instance,
            requireSchemaValidation: true);

        RegressionAssert.True(
            identity.SchemaValidation.IsValid
            && identity.SchemaValidation.Issues.Count == 0,
            "A classified source used document-local URI spelling instead of its manifest schema binding.");

        Console.WriteLine("[PASS] Manifest family binding is the deterministic schema authority");
    }

    private static void TestFamilyShapeAndRequiredPropertyIssuesAreStable()
    {
        var sources = new[]
        {
            new MechanicalContentSourceDocument(
                "registry.professions",
                "content/registries/professions.json",
                "profession",
                "{}",
                FamilyId: "content.registry.professions",
                SchemaId: "source.object.schema.json",
                RequiresSourceFamilyManifest: true),
            new MechanicalContentSourceDocument(
                "core.creatures",
                "data/core/creatures/invalid.json",
                "creature",
                "{}",
                FamilyId: "core.creature",
                SchemaId: "source.definition-array.schema.json",
                RequiresSourceFamilyManifest: true),
        };
        var schemas = new[]
        {
            new ContentSchemaSourceDocument(
                "content/schemas/source.object.schema.json",
                """{"$schema":"http://json-schema.org/draft-07/schema#","type":"object"}"""),
            new ContentSchemaSourceDocument(
                "content/schemas/source.definition-array.schema.json",
                """{"$schema":"http://json-schema.org/draft-07/schema#","type":"array"}"""),
        };
        var first = MechanicalContentIdentityCompiler.Compile(
            sources,
            schemas,
            JsonSchemaNetContentValidationAdapter.Instance,
            requireSchemaValidation: true);
        var second = MechanicalContentIdentityCompiler.Compile(
            sources.Reverse(),
            schemas.Reverse(),
            JsonSchemaNetContentValidationAdapter.Instance,
            requireSchemaValidation: true);

        RegressionAssert.True(
            first.SchemaValidation.Issues.SequenceEqual(second.SchemaValidation.Issues)
            && first.SchemaValidation.Issues.Any(static issue =>
                issue.Code == "Content.Schema.FamilyRequiredPropertyMissing"
                && issue.Source == "content/registries/professions.json")
            && first.SchemaValidation.Issues.Any(static issue =>
                issue.Code == "Content.Schema.FamilyRootMismatch"
                && issue.Source == "data/core/creatures/invalid.json"),
            "Family shape or required-property diagnostics depend on source/schema enumeration order.");

        Console.WriteLine("[PASS] Family root and required-property diagnostics are stable");
    }
}
