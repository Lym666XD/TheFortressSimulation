using HumanFortress.Content.Identity;
using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Identity;

internal static class ContentMechanicalIdentityRegressionTests
{
    internal static void RunAll()
    {
        TestFilePropertyAndInsertionPermutationIsCanonical();
        TestMechanicalAndCosmeticChangePolicy();
        TestEveryManifestFamilyHasAConservativeCanonicalPolicy();
        TestHandlesAreDeterministicReversibleAndCollisionChecked();
        TestObjectMapAndAttachmentHandlesAreReversible();
        TestIssuesAreStableAndReferencesFailClosed();
        TestRepresentativeCrossFamilyReferencesFailClosed();
        TestPinnedSchemaAdapterValidatesAndResolvesLocally();
        TestSchemaFailuresAndIssueOrderingAreStable();
        TestStrictSchemaAdapterFailsClosedWhenUnavailable();
        TestLoadedContentCarriesMechanicalIdentity();
    }

    private static void TestFilePropertyAndInsertionPermutationIsCanonical()
    {
        var first = MechanicalContentIdentityCompiler.Compile(CreateEquivalentSources(reversed: false));
        var second = MechanicalContentIdentityCompiler.Compile(
            CreateEquivalentSources(reversed: true).Reverse());

        RegressionAssert.True(
            !first.HasMechanicalErrors
            && !second.HasMechanicalErrors
            && first.MechanicalSignature == second.MechanicalSignature
            && first.Sections.SequenceEqual(second.Sections)
            && first.LocalHandles.SequenceEqual(second.LocalHandles),
            "File/property/definition/set insertion order changed canonical content identity or handles.");

        Console.WriteLine("[PASS] Mechanical content identity ignores file and insertion permutation");
    }

    private static void TestMechanicalAndCosmeticChangePolicy()
    {
        var baseline = MechanicalContentIdentityCompiler.Compile(CreateEquivalentSources(reversed: false));
        var mechanical = MechanicalContentIdentityCompiler.Compile(
            CreateEquivalentSources(reversed: false, density: 7_900));
        var cosmetic = MechanicalContentIdentityCompiler.Compile(
            CreateEquivalentSources(
                reversed: false,
                description: "Different prose",
                displayGlyph: 99));

        RegressionAssert.True(
            baseline.MechanicalSignature != mechanical.MechanicalSignature
            && baseline.MechanicalSignature == cosmetic.MechanicalSignature
            && baseline.CosmeticPolicyId == CanonicalMechanicalJsonSerializer.CosmeticPolicyId,
            "Mechanical field changes were omitted or explicit cosmetic fields changed compatibility identity.");

        Console.WriteLine("[PASS] Mechanical and cosmetic content hash policy is explicit");
    }

    private static void TestEveryManifestFamilyHasAConservativeCanonicalPolicy()
    {
        foreach (var family in MechanicalContentSourceFamilyManifest.Declarations)
        {
            var policy = MechanicalContentCanonicalPolicy.Resolve(family.FamilyId);
            RegressionAssert.True(
                policy.FamilyId == family.FamilyId,
                $"Content family '{family.FamilyId}' has no canonical policy.");
        }

        var itemBaseline = CompileSingle(
            "core.item",
            "core.items",
            "item",
            """[{"id":"core_item_test","name":"Test Item","tags":["tool","test"],"mechanical_extension":1}]""");
        var itemCosmeticAndSetPermutation = CompileSingle(
            "core.item",
            "core.items",
            "item",
            """[{"tags":["test","tool"],"name":"Renamed Item","mechanical_extension":1,"id":"core_item_test"}]""");
        var itemMechanicalExtension = CompileSingle(
            "core.item",
            "core.items",
            "item",
            """[{"id":"core_item_test","name":"Test Item","tags":["tool","test"],"mechanical_extension":2}]""");
        var orderedBands = CompileSingle(
            "content.registry.tuning.mapgen",
            "registry.tuning.mapgen",
            "tuning.mapgen",
            """{"bands":[{"threshold":1},{"threshold":2}]}""");
        var reversedBands = CompileSingle(
            "content.registry.tuning.mapgen",
            "registry.tuning.mapgen",
            "tuning.mapgen",
            """{"bands":[{"threshold":2},{"threshold":1}]}""");
        var terrainName = CompileSingle(
            "content.registry.terrain-kinds",
            "registry.terrain_kinds",
            "terrain",
            """{"terrainKinds":[{"id":0,"name":"solid_wall"}]}""");
        var changedTerrainName = CompileSingle(
            "content.registry.terrain-kinds",
            "registry.terrain_kinds",
            "terrain",
            """{"terrainKinds":[{"id":0,"name":"solid_wall_changed"}]}""");

        RegressionAssert.True(
            itemBaseline.MechanicalSignature == itemCosmeticAndSetPermutation.MechanicalSignature
            && itemBaseline.MechanicalSignature != itemMechanicalExtension.MechanicalSignature
            && orderedBands.MechanicalSignature != reversedBands.MechanicalSignature
            && terrainName.MechanicalSignature != changedTerrainName.MechanicalSignature,
            "Family-aware cosmetic, unordered-set, ordered-sequence, unknown-field, or terrain-id policy drifted.");

        Console.WriteLine("[PASS] Every source family has a conservative cosmetic and sequence policy");
    }

    private static void TestHandlesAreDeterministicReversibleAndCollisionChecked()
    {
        var identity = MechanicalContentIdentityCompiler.Compile(CreateEquivalentSources(reversed: true));
        var hasItem = identity.TryGetLocalHandle("item", "core_item_ingot_iron", out var itemHandle);
        var resolvesItem = identity.TryResolveLocalHandle(itemHandle, out var resolvedItem);
        var orderedHandles = identity.LocalHandles
            .Select(static row => row.QualifiedId)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        RegressionAssert.True(
            hasItem
            && itemHandle != 0
            && resolvesItem
            && resolvedItem?.QualifiedId == "item:core_item_ingot_iron"
            && identity.LocalHandles.Select(static row => row.Handle)
                .SequenceEqual(Enumerable.Range(1, identity.LocalHandles.Count).Select(static value => (uint)value))
            && identity.LocalHandles.Select(static row => row.QualifiedId).SequenceEqual(orderedHandles),
            "Compiled content handles were not ordinal, contiguous, or reversible to canonical ids.");

        Console.WriteLine("[PASS] Mechanical content handles are deterministic and reversible");
    }

    private static void TestObjectMapAndAttachmentHandlesAreReversible()
    {
        var identity = MechanicalContentIdentityCompiler.Compile(
            new[]
            {
                new MechanicalContentSourceDocument(
                    "core.workshops",
                    "workshops.json",
                    "construction",
                    """
                    {
                      "workshops":[{"id":"core_workshop_test"}],
                      "attachments":[{"id":"core_attachment_test","upgrade_to":null}]
                    }
                    """),
                new MechanicalContentSourceDocument(
                    "core.recipes",
                    "recipes.json",
                    "recipe",
                    """
                    {
                      "recipes":[{
                        "id":"core_recipe_test",
                        "workshops":["core_workshop_test"],
                        "requirements":{"enablers":["core_attachment_test"]},
                        "inputs":[],
                        "outputs":[]
                      }]
                    }
                    """),
                new MechanicalContentSourceDocument(
                    "registry.orders",
                    "orders.json",
                    "order",
                    """
                    {
                      "orders":{
                        "mining":{
                          "id":"orders.mining",
                          "tools":{"dig":{"id":"orders.mining.dig"}}
                        }
                      }
                    }
                    """)
            });

        var qualifiedIds = identity.LocalHandles
            .Select(static row => row.QualifiedId)
            .ToHashSet(StringComparer.Ordinal);
        RegressionAssert.True(
            !identity.HasMechanicalErrors
            && qualifiedIds.Contains("construction:core_workshop_test")
            && qualifiedIds.Contains("construction.attachment:core_attachment_test")
            && qualifiedIds.Contains("recipe:core_recipe_test")
            && qualifiedIds.Contains("order:orders.mining")
            && qualifiedIds.Contains("order:orders.mining.dig")
            && identity.LocalHandles.All(row =>
                identity.TryResolveLocalHandle(row.Handle, out var resolved)
                && resolved == row),
            "Object-map, nested-tool, recipe, workshop, or attachment handles were missing or irreversible.");

        Console.WriteLine("[PASS] Object-map and attachment handles are deterministic and reversible");
    }

    private static void TestIssuesAreStableAndReferencesFailClosed()
    {
        var sources = new[]
        {
            new MechanicalContentSourceDocument(
                "core.items", "a.json", "item",
                """[{"id":"core_item_dup","base_volume_ml":1}]"""),
            new MechanicalContentSourceDocument(
                "core.items", "b.json", "item",
                """[{"id":"core_item_dup","base_volume_ml":2},{"id":"core_item_case"},{"id":"core_item_Case"},{"id":"1invalid"}]"""),
            new MechanicalContentSourceDocument(
                "core.recipes", "c.json", "recipe",
                """{"recipes":[{"id":"core_recipe_missing","inputs":[{"def_id":"core_item_missing","count":1}],"outputs":[]}]}""")
        };
        var first = MechanicalContentIdentityCompiler.Compile(sources);
        var second = MechanicalContentIdentityCompiler.Compile(sources.Reverse());

        RegressionAssert.True(
            first.HasMechanicalErrors
            && first.Issues.SequenceEqual(second.Issues)
            && first.Issues.Any(static issue => issue.Code == "Content.Identity.DuplicateId")
            && first.Issues.Any(static issue => issue.Code == "Content.Identity.AmbiguousId")
            && first.Issues.Any(static issue => issue.Code == "Content.Identity.InvalidId")
            && first.Issues.Any(static issue => issue.Code == "Content.Reference.Missing"),
            "Duplicate, ambiguous, invalid, or missing-reference content did not fail closed with stable issues.");

        Console.WriteLine("[PASS] Mechanical content issues are stable and fail closed");
    }

    private static void TestRepresentativeCrossFamilyReferencesFailClosed()
    {
        var sources = new[]
        {
            new MechanicalContentSourceDocument(
                "registry.body-plans",
                "body-plans.json",
                "creature.body_plan",
                """[{"id":"humanoid_simple","slots":[]}]"""),
            new MechanicalContentSourceDocument(
                "registry.creatures",
                "creatures.json",
                "creature.registry",
                """[{"id":"cre_test","body_plan_id":"body_plan_missing"}]"""),
            new MechanicalContentSourceDocument(
                "registry.geology",
                "geology.json",
                "geology",
                """[{"id":"geo_test","material":"material_missing"}]"""),
            new MechanicalContentSourceDocument(
                "registry.stockpile-presets",
                "stockpile-presets.json",
                "stockpile.preset",
                """[{"id":"preset_test","itemIds":["item_missing"],"materials":["material_missing"]}]"""),
            new MechanicalContentSourceDocument(
                "registry.tuning-mining",
                "tuning-mining.json",
                "registry.tuning.mining",
                """{"drops":[{"item_id":"item_missing"}]}"""),
            new MechanicalContentSourceDocument(
                "core.constructions",
                "constructions.json",
                "construction",
                """{"constructions":[{"id":"core_construction_test","result_material_id":"material_result_missing"}]}"""),
            new MechanicalContentSourceDocument(
                "registry.tuning-ore",
                "tuning-ore.json",
                "tuning.ore",
                """{"ores":[{"id":"geology_ore_missing"}]}""")
        };

        var first = MechanicalContentIdentityCompiler.Compile(sources);
        var second = MechanicalContentIdentityCompiler.Compile(sources.Reverse());
        var missing = first.Issues
            .Where(static issue => issue.Code == "Content.Reference.Missing")
            .ToArray();

        RegressionAssert.True(
            first.HasMechanicalErrors
            && first.Issues.SequenceEqual(second.Issues)
            && missing.Length == 7
            && missing.Any(static issue => issue.Message.Contains(
                "creature.body_plan:body_plan_missing",
                StringComparison.Ordinal))
            && missing.Any(static issue => issue.Message.Contains(
                "item:item_missing",
                StringComparison.Ordinal))
            && missing.Any(static issue => issue.Message.Contains(
                "material:material_missing",
                StringComparison.Ordinal))
            && missing.Any(static issue => issue.Message.Contains(
                "material:material_result_missing",
                StringComparison.Ordinal))
            && missing.Any(static issue => issue.Message.Contains(
                "geology:geology_ore_missing",
                StringComparison.Ordinal)),
            "Representative creature, geology, tuning, item, or material references did not fail closed deterministically.");

        Console.WriteLine("[PASS] Representative cross-family references fail closed with stable diagnostics");
    }

    private static void TestStrictSchemaAdapterFailsClosedWhenUnavailable()
    {
        var identity = MechanicalContentIdentityCompiler.Compile(
            new[]
            {
                new MechanicalContentSourceDocument(
                    "core.items", "items.json", "item", """[{"id":"core_item_test"}]""")
            },
            new[]
            {
                new ContentSchemaSourceDocument("items.schema.json", """{"$schema":"http://json-schema.org/draft-07/schema#"}""")
            },
            UnavailableContentJsonSchemaValidationAdapter.Instance,
            requireSchemaValidation: true);

        RegressionAssert.True(
            identity.SchemaValidation.Availability == ContentSchemaValidatorAvailability.Unavailable
            && !identity.SchemaValidation.IsValid
            && identity.HasMechanicalErrors
            && identity.Issues.Any(static issue => issue.Code == "Content.Schema.ValidatorUnavailable"),
            "Strict content identity silently treated JSON parsing as standards-compliant schema validation.");

        Console.WriteLine("[PASS] Strict schema validation fails closed when its standard adapter is unavailable");
    }

    private static void TestPinnedSchemaAdapterValidatesAndResolvesLocally()
    {
        var identity = MechanicalContentIdentityCompiler.Compile(
            new[]
            {
                new MechanicalContentSourceDocument(
                    "test.entries",
                    "data/core/test/valid.json",
                    "test",
                    """
                    {
                      "$schema":"https://humanfortress.game/schemas/test-entry.schema.json",
                      "id":"test_entry_valid",
                      "count":4
                    }
                    """)
            },
            CreateTestSchemas(),
            JsonSchemaNetContentValidationAdapter.Instance,
            requireSchemaValidation: true);

        RegressionAssert.True(
            identity.SchemaValidation.Availability == ContentSchemaValidatorAvailability.Available
            && identity.SchemaValidation.AdapterId == JsonSchemaNetContentValidationAdapter.StableAdapterId
            && identity.SchemaValidation.IsValid
            && identity.SchemaValidation.Issues.Count == 0,
            "Pinned schema validation did not accept a valid document with a local cross-schema reference.");

        Console.WriteLine("[PASS] Pinned JSON Schema adapter validates through its local registry");
    }

    private static void TestSchemaFailuresAndIssueOrderingAreStable()
    {
        var sources = new[]
        {
            new MechanicalContentSourceDocument(
                "test.entries",
                "data/core/test/unresolved.json",
                "test",
                """{"$schema":"https://outside.invalid/unknown.schema.json","id":"test_entry_unknown","count":1}"""),
            new MechanicalContentSourceDocument(
                "test.entries",
                "data/core/test/invalid.json",
                "test",
                """{"$schema":"https://humanfortress.game/schemas/test-entry.schema.json","id":"test_entry_invalid","count":"four"}"""),
            new MechanicalContentSourceDocument(
                "test.legacy",
                "data/core/test/legacy.json",
                "test",
                """[{"id":"test_entry_legacy","count":1}]""")
        };
        var schemas = CreateTestSchemas();
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
        var invalidSchema = MechanicalContentIdentityCompiler.Compile(
            new[]
            {
                new MechanicalContentSourceDocument(
                    "test.entries",
                    "data/core/test/schema-invalid.json",
                    "test",
                    """{"$schema":"https://humanfortress.game/schemas/invalid.schema.json","id":"test_entry_schema_invalid","count":1}""")
            },
            new[]
            {
                new ContentSchemaSourceDocument(
                    "invalid.schema.json",
                    """{"$schema":"http://json-schema.org/draft-07/schema#","$id":"https://humanfortress.game/schemas/invalid.schema.json","type":42}""")
            },
            JsonSchemaNetContentValidationAdapter.Instance,
            requireSchemaValidation: true);

        var ordered = first.SchemaValidation.Issues
            .OrderBy(static issue => issue.Source, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Path, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Message, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Severity)
            .ToArray();

        RegressionAssert.True(
            !first.SchemaValidation.IsValid
            && first.SchemaValidation.Issues.SequenceEqual(second.SchemaValidation.Issues)
            && first.SchemaValidation.Issues.SequenceEqual(ordered)
            && first.SchemaValidation.Issues.Any(static issue =>
                issue.Source == "data/core/test/invalid.json"
                && issue.Code == "Content.Schema.ValidationFailed")
            && first.SchemaValidation.Issues.Any(static issue =>
                issue.Source == "data/core/test/unresolved.json"
                && issue.Code == "Content.Schema.ExplicitResolutionFailed")
            && first.SchemaValidation.Issues.Any(static issue =>
                issue.Source == "data/core/test/legacy.json"
                && issue.Code == "Content.Schema.SourceUnsupported")
            && invalidSchema.SchemaValidation.Issues.Any(static issue =>
                issue.Source == "invalid.schema.json"
                && issue.Code == "Content.Schema.Invalid"),
            "Schema validation, explicit resolution, unsupported-source, or invalid-schema failures were not stable and fail closed.");

        Console.WriteLine("[PASS] Schema failures and diagnostics are deterministic and fail closed");
    }

    private static void TestLoadedContentCarriesMechanicalIdentity()
    {
        var first = FortressContentLoader.Load(AppContext.BaseDirectory);
        var second = FortressContentLoader.Load(AppContext.BaseDirectory);
        var identity = first.MechanicalIdentity;

        RegressionAssert.True(
            identity != null
            && identity.FormatId == CanonicalMechanicalJsonSerializer.FormatId
            && identity.FormatVersion == MechanicalContentIdentityCompiler.FormatVersion
            && identity.MechanicalSignature.Length == 64
            && identity.Sections.Count > 0
            && identity.LocalHandles.Count > 0
            && second.MechanicalIdentity?.MechanicalSignature == identity.MechanicalSignature
            && identity.Sections.SequenceEqual(
                identity.Sections.OrderBy(static section => section.SectionId, StringComparer.Ordinal))
            && identity.Issues.SequenceEqual(identity.Issues
                .OrderBy(static issue => issue.Source, StringComparer.Ordinal)
                .ThenBy(static issue => issue.Path, StringComparer.Ordinal)
                .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(static issue => issue.Message, StringComparer.Ordinal)
                .ThenBy(static issue => issue.Severity)),
            "Content bootstrap did not carry a repeatable canonical mechanical identity snapshot.");

        Console.WriteLine("[PASS] Loaded content carries repeatable mechanical identity");
    }

    private static MechanicalContentSourceDocument[] CreateEquivalentSources(
        bool reversed,
        int density = 7_800,
        string description = "Iron material",
        int displayGlyph = 35)
    {
        var materialJson = reversed
            ? $$"""[{"tags":["metal","iron"],"display":{"glyph":{{displayGlyph}}},"description":"{{description}}","density":{{density}},"id":"core_mat_iron"},{"id":"core_mat_copper","density":8900,"tags":["copper","metal"]}]"""
            : $$"""[{"id":"core_mat_copper","tags":["metal","copper"],"density":8900},{"id":"core_mat_iron","density":{{density}},"description":"{{description}}","display":{"glyph":{{displayGlyph}}},"tags":["iron","metal"]}]""";
        var itemJson = reversed
            ? """{"items":[{"tags":["metal","ingot"],"base_volume_ml":100,"fixed_material":"core_mat_iron","id":"core_item_ingot_iron"},{"id":"core_item_ingot_copper","fixed_material":"core_mat_copper","base_volume_ml":100,"tags":["ingot","metal"]}]}"""
            : """{"items":[{"id":"core_item_ingot_copper","tags":["metal","ingot"],"base_volume_ml":100,"fixed_material":"core_mat_copper"},{"id":"core_item_ingot_iron","fixed_material":"core_mat_iron","tags":["ingot","metal"],"base_volume_ml":100}]}""";

        return new[]
        {
            new MechanicalContentSourceDocument(
                "registry.materials", reversed ? "renamed-material-file.json" : "materials.json", "material", materialJson),
            new MechanicalContentSourceDocument(
                "core.items", reversed ? "renamed-item-file.json" : "items.json", "item", itemJson)
        };
    }

    private static ContentSchemaSourceDocument[] CreateTestSchemas()
    {
        return new[]
        {
            new ContentSchemaSourceDocument(
                "a.test-entry.schema.json",
                """
                {
                  "$schema":"http://json-schema.org/draft-07/schema#",
                  "$id":"https://humanfortress.game/schemas/test-entry.schema.json",
                  "type":"object",
                  "required":["$schema","id","count"],
                  "properties":{
                    "$schema":{"type":"string"},
                    "id":{"type":"string","pattern":"^test_entry_[a-z0-9_]+$"},
                    "count":{"$ref":"https://humanfortress.game/schemas/test-count.schema.json"}
                  },
                  "additionalProperties":false
                }
                """),
            new ContentSchemaSourceDocument(
                "z.test-count.schema.json",
                """
                {
                  "$schema":"http://json-schema.org/draft-07/schema#",
                  "$id":"https://humanfortress.game/schemas/test-count.schema.json",
                  "type":"integer",
                  "minimum":1
                }
                """)
        };
    }

    private static MechanicalContentIdentityData CompileSingle(
        string familyId,
        string sectionId,
        string handleNamespace,
        string json)
    {
        return MechanicalContentIdentityCompiler.Compile(
            new[]
            {
                new MechanicalContentSourceDocument(
                    sectionId,
                    familyId + ".json",
                    handleNamespace,
                    json,
                    FamilyId: familyId)
            });
    }
}
