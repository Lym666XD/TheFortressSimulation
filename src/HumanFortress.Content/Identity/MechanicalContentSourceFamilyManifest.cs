namespace HumanFortress.Content.Identity;

internal enum ContentSourceFamilyMatchKind : byte
{
    Exact = 0,
    PrefixAndJsonSuffix = 1,
}

internal enum ContentSourceRootShape : byte
{
    Object = 0,
    Array = 1,
    ObjectOrArray = 2,
}

internal enum ContentSourceFamilyActivationPolicy : byte
{
    Active = 0,
    Excluded = 1,
    ExcludedWhenAnySupersedingSourceExists = 2,
    ExcludedWhenAllSupersedingSourcesExist = 3,
}

internal sealed record ContentSourceFamilyDeclaration(
    string FamilyId,
    string SourcePattern,
    ContentSourceFamilyMatchKind MatchKind,
    string SectionId,
    string HandleNamespace,
    string? SchemaId,
    ContentSourceRootShape RootShape,
    IReadOnlyList<string> RequiredRootProperties,
    string? DefinitionCollectionProperty,
    ContentSourceFamilyActivationPolicy ActivationPolicy = ContentSourceFamilyActivationPolicy.Active,
    string? ExclusionReason = null,
    IReadOnlyList<string>? SupersedingSourcePatterns = null);

internal sealed record ContentSourceFamilyResolution(
    string FamilyId,
    string SectionId,
    string HandleNamespace,
    string? SchemaId,
    ContentSourceRootShape RootShape,
    IReadOnlyList<string> RequiredRootProperties,
    string? DefinitionCollectionProperty,
    bool IsActive,
    string? ExclusionReason);

/// <summary>
/// Content-owned inventory of JSON sources that can affect a composed session.
/// Unknown files are deliberately absent: the scanner marks them unclassified
/// and strict schema validation rejects them.
/// </summary>
internal static class MechanicalContentSourceFamilyManifest
{
    internal const string CanonicalPolicyId = "humanfortress.content-source-families.v2";

    private const string ObjectSchema = "source.object.schema.json";
    private const string DefinitionArraySchema = "source.definition-array.schema.json";

    private static readonly IReadOnlyList<ContentSourceFamilyDeclaration> ManifestDeclarations =
        Array.AsReadOnly(new[]
        {
            ExactExcluded(
                "content.registry.input-bindings",
                "content/registries/input.bindings.json",
                "registry.input.bindings",
                "input.binding",
                "cosmetic-input-policy"),
            ExactExcluded(
                "content.registry.workshop-category-ui",
                "content/registries/ui.workshop_categories.json",
                "registry.ui.workshop_categories",
                "ui.workshop-category",
                "cosmetic-ui-policy"),
            ExactExcluded(
                "content.registry.creature-body-plan-legacy",
                "content/registries/creatures.body_plans.json",
                "registry.creatures.body_plans",
                "creature.body_plan",
                "inactive-unimplemented-source"),
            ExactExcluded(
                "content.registry.creature-legacy",
                "content/registries/creatures.json",
                "registry.creatures",
                "creature.registry",
                "inactive-superseded-source"),
            ExactExcluded(
                "content.registry.orders-legacy",
                "content/registries/orders.registry.json",
                "registry.orders.registry",
                "order",
                "inactive-unimplemented-source"),
            ExactExcluded(
                "content.registry.stockpiles-legacy",
                "content/registries/stockpiles.json",
                "registry.stockpiles",
                "stockpile.definition",
                "inactive-superseded-source"),
            ExactExcluded(
                "content.registry.tuning.damage-inactive",
                "content/registries/tuning.damage.json",
                "registry.tuning.damage",
                "tuning.damage",
                "inactive-unimplemented-source"),
            ExactExcluded(
                "content.registry.tuning.hauling-inactive",
                "content/registries/tuning.hauling.json",
                "registry.tuning.hauling",
                "tuning.hauling",
                "inactive-unimplemented-source"),
            ExactExcluded(
                "content.registry.tuning.stockpile-inactive",
                "content/registries/tuning.stockpile.json",
                "registry.tuning.stockpile",
                "tuning.stockpile",
                "inactive-unimplemented-source"),
            ExactExcluded(
                "content.registry.tuning.tile-inactive",
                "content/registries/tuning.tile.json",
                "registry.tuning.tile",
                "tuning.tile",
                "inactive-unimplemented-source"),
            ExactConditional(
                "content.registry.material-compiled",
                "content/registries/materials.registry.json",
                "registry.materials.registry",
                "material",
                "materials.registry.schema.json",
                ContentSourceRootShape.Array,
                null,
                "inactive-generated-source",
                ContentSourceFamilyActivationPolicy.ExcludedWhenAnySupersedingSourceExists,
                "content/registries/materials.authoring.json"),
            ExactConditional(
                "content.registry.geology-fallback",
                "content/registries/geology_prototypes.json",
                "registry.geology_prototypes",
                "geology",
                ObjectSchema,
                ContentSourceRootShape.Object,
                "prototypes",
                "inactive-fallback-source",
                ContentSourceFamilyActivationPolicy.ExcludedWhenAnySupersedingSourceExists,
                "content/registries/geology.json",
                requiredRootProperties: new[] { "version", "prototypes" }),
            ExactConditional(
                "core.item.legacy-weapons",
                "data/core/items/weapons.json",
                "core.items",
                "item",
                "source.item-catalog.schema.json",
                ContentSourceRootShape.ObjectOrArray,
                "items",
                "inactive-legacy-source",
                ContentSourceFamilyActivationPolicy.ExcludedWhenAllSupersedingSourcesExist,
                "data/core/items/weapons_melee.json",
                "data/core/items/weapons_ranged.json"),
            new ContentSourceFamilyDeclaration(
                "core.construction.legacy-workshops",
                "data/core/placeable/workshops.json",
                ContentSourceFamilyMatchKind.Exact,
                "core.placeable",
                "construction",
                "source.construction-catalog.schema.json",
                ContentSourceRootShape.Object,
                new[] { "version", "constructions" },
                "constructions",
                ContentSourceFamilyActivationPolicy.ExcludedWhenAnySupersedingSourceExists,
                "inactive-legacy-source",
                new[] { "data/core/workshops/" }),

            ExactActive("content.registry.geology", "content/registries/geology.json", "registry.geology", "geology", "source.geology-catalog.schema.json", ContentSourceRootShape.Array),
            ExactActive("content.registry.material-authoring", "content/registries/materials.authoring.json", "registry.materials.authoring", "material", "material.authoring.schema.json", ContentSourceRootShape.Array),
            ExactActive("content.registry.professions", "content/registries/professions.json", "registry.professions", "profession", "source.profession-catalog.schema.json", ContentSourceRootShape.Object, "professions", new[] { "professions" }),
            ExactActive("content.registry.stockpile-presets", "content/registries/stockpile_presets.json", "registry.stockpile_presets", "stockpile.preset", "source.stockpile-preset-catalog.schema.json", ContentSourceRootShape.Array),
            ExactActive("content.registry.terrain-kinds", "content/registries/terrain_kinds.json", "registry.terrain_kinds", "terrain", "source.terrain-kinds.schema.json", ContentSourceRootShape.Object, requiredRootProperties: new[] { "version", "terrainKinds", "terrainBitLayout" }),
            ExactActive("content.registry.zones", "content/registries/zones.json", "registry.zones", "zone", "source.zone-catalog.schema.json", ContentSourceRootShape.Object, "zones", new[] { "zones" }),

            Tuning("cavern", "band", "path", "rooms", "shafts"),
            Tuning("mapgen", "surface", "hills", "bands"),
            Tuning("mining", "geology_drops", "geology_ticks", "tool_multipliers"),
            Tuning("navigation", "allow_diagonals", "cost", "movement", "budgets"),
            Tuning("ore", "global", "ores"),
            Tuning("placeable", "$schema", "version", "quality", "construction"),
            Tuning("scheduler", "version", "threads", "priorities", "budgets"),
            Tuning("workshops", "max_queued_recipes_default", "craft_ticks_per_volume"),

            PrefixActive("content.template.biome", "content/templates/", "template.biome", "biome", "biome_template.schema.json", ContentSourceRootShape.Object, "templates", "version", "templates"),
            PrefixActive("core.creature", "data/core/creatures/", "core.creatures", "creature", DefinitionArraySchema, ContentSourceRootShape.Array),
            PrefixActive("core.item", "data/core/items/", "core.items", "item", "source.item-catalog.schema.json", ContentSourceRootShape.ObjectOrArray, "items"),
            PrefixActive("core.recipe", "data/core/recipes/core_", "core.recipes", "recipe", "source.recipe-catalog.schema.json", ContentSourceRootShape.ObjectOrArray, "recipes"),
            PrefixExcluded("core.recipe.draft", "data/core/recipes/recipes.", "core.recipes.draft", "recipe", "inactive-draft-source"),
            PrefixActive("core.construction", "data/core/placeable/constructions", "core.placeable", "construction", "source.construction-catalog.schema.json", ContentSourceRootShape.Object, "constructions", "version", "constructions"),
            ExactExcluded("core.placeable.terrain-designation-inactive", "data/core/placeable/terrain_designations.json", "core.placeable", "placeable", "inactive-unimplemented-source"),
            ExactExcluded("core.placeable.workshop-attachment-inactive", "data/core/placeable/workshop_attachments.json", "core.placeable", "placeable", "inactive-unimplemented-source"),
            PrefixActive("core.workshop", "data/core/workshops/core_workshop_", "core.workshops", "construction", "source.workshop-catalog.schema.json", ContentSourceRootShape.Object, "workshops", "version", "workshops"),
        }
        .OrderBy(static declaration => declaration.FamilyId, StringComparer.Ordinal)
        .ToArray());

    internal static IReadOnlyList<ContentSourceFamilyDeclaration> Declarations => ManifestDeclarations;

    internal static bool TryResolve(
        string sourceId,
        IReadOnlySet<string> availableSourceIds,
        out ContentSourceFamilyResolution resolution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentNullException.ThrowIfNull(availableSourceIds);

        var normalized = Normalize(sourceId);
        var matches = ManifestDeclarations
            .Where(declaration => Matches(declaration, normalized))
            .OrderBy(static declaration => declaration.MatchKind)
            .ThenByDescending(static declaration => declaration.SourcePattern.Length)
            .ThenBy(static declaration => declaration.FamilyId, StringComparer.Ordinal)
            .ToArray();
        if (matches.Length == 0)
        {
            resolution = null!;
            return false;
        }

        var declaration = matches[0];
        var excluded = IsExcluded(declaration, availableSourceIds);
        resolution = new ContentSourceFamilyResolution(
            declaration.FamilyId,
            declaration.SectionId,
            declaration.HandleNamespace,
            declaration.SchemaId,
            declaration.RootShape,
            declaration.RequiredRootProperties,
            declaration.DefinitionCollectionProperty,
            !excluded,
            excluded ? declaration.ExclusionReason : null);
        return true;
    }

    internal static bool TryGetDeclaration(
        string familyId,
        out ContentSourceFamilyDeclaration declaration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);
        declaration = ManifestDeclarations.FirstOrDefault(value =>
            value.FamilyId.Equals(familyId, StringComparison.Ordinal))!;
        return declaration != null;
    }

    private static bool Matches(ContentSourceFamilyDeclaration declaration, string sourceId)
    {
        return declaration.MatchKind switch
        {
            ContentSourceFamilyMatchKind.Exact =>
                sourceId.Equals(declaration.SourcePattern, StringComparison.Ordinal),
            ContentSourceFamilyMatchKind.PrefixAndJsonSuffix =>
                sourceId.StartsWith(declaration.SourcePattern, StringComparison.Ordinal)
                && sourceId.EndsWith(".json", StringComparison.Ordinal),
            _ => false,
        };
    }

    private static bool IsExcluded(
        ContentSourceFamilyDeclaration declaration,
        IReadOnlySet<string> availableSourceIds)
    {
        var normalizedAvailable = availableSourceIds is HashSet<string> hashSet
                                  && hashSet.Comparer.Equals(StringComparer.Ordinal)
            ? hashSet
            : availableSourceIds.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        var superseding = declaration.SupersedingSourcePatterns ?? Array.Empty<string>();
        return declaration.ActivationPolicy switch
        {
            ContentSourceFamilyActivationPolicy.Active => false,
            ContentSourceFamilyActivationPolicy.Excluded => true,
            ContentSourceFamilyActivationPolicy.ExcludedWhenAnySupersedingSourceExists =>
                superseding.Any(pattern => SourcePatternExists(pattern, normalizedAvailable)),
            ContentSourceFamilyActivationPolicy.ExcludedWhenAllSupersedingSourcesExist =>
                superseding.Count > 0
                && superseding.All(pattern => SourcePatternExists(pattern, normalizedAvailable)),
            _ => throw new InvalidOperationException(
                $"Unsupported content source activation policy '{declaration.ActivationPolicy}'."),
        };
    }

    private static bool SourcePatternExists(string pattern, IReadOnlySet<string> availableSourceIds)
    {
        var normalized = Normalize(pattern);
        return normalized.EndsWith("/", StringComparison.Ordinal)
            ? availableSourceIds.Any(source => source.StartsWith(normalized, StringComparison.Ordinal))
            : availableSourceIds.Contains(normalized);
    }

    private static string Normalize(string value) => value.Replace('\\', '/').TrimStart('/');

    private static ContentSourceFamilyDeclaration ExactActive(
        string familyId,
        string sourceId,
        string sectionId,
        string handleNamespace,
        string schemaId,
        ContentSourceRootShape rootShape,
        string? definitionCollectionProperty = null,
        IReadOnlyList<string>? requiredRootProperties = null)
    {
        return new ContentSourceFamilyDeclaration(
            familyId,
            sourceId,
            ContentSourceFamilyMatchKind.Exact,
            sectionId,
            handleNamespace,
            schemaId,
            rootShape,
            requiredRootProperties ?? Array.Empty<string>(),
            definitionCollectionProperty);
    }

    private static ContentSourceFamilyDeclaration PrefixActive(
        string familyId,
        string sourcePrefix,
        string sectionId,
        string handleNamespace,
        string schemaId,
        ContentSourceRootShape rootShape,
        string? definitionCollectionProperty = null,
        params string[] requiredRootProperties)
    {
        return new ContentSourceFamilyDeclaration(
            familyId,
            sourcePrefix,
            ContentSourceFamilyMatchKind.PrefixAndJsonSuffix,
            sectionId,
            handleNamespace,
            schemaId,
            rootShape,
            requiredRootProperties,
            definitionCollectionProperty);
    }

    private static ContentSourceFamilyDeclaration ExactExcluded(
        string familyId,
        string sourceId,
        string sectionId,
        string handleNamespace,
        string reason)
    {
        return new ContentSourceFamilyDeclaration(
            familyId,
            sourceId,
            ContentSourceFamilyMatchKind.Exact,
            sectionId,
            handleNamespace,
            null,
            ContentSourceRootShape.Object,
            Array.Empty<string>(),
            null,
            ContentSourceFamilyActivationPolicy.Excluded,
            reason);
    }

    private static ContentSourceFamilyDeclaration PrefixExcluded(
        string familyId,
        string sourcePrefix,
        string sectionId,
        string handleNamespace,
        string reason)
    {
        return new ContentSourceFamilyDeclaration(
            familyId,
            sourcePrefix,
            ContentSourceFamilyMatchKind.PrefixAndJsonSuffix,
            sectionId,
            handleNamespace,
            SchemaId: null,
            ContentSourceRootShape.ObjectOrArray,
            Array.Empty<string>(),
            DefinitionCollectionProperty: null,
            ContentSourceFamilyActivationPolicy.Excluded,
            reason);
    }

    private static ContentSourceFamilyDeclaration ExactConditional(
        string familyId,
        string sourceId,
        string sectionId,
        string handleNamespace,
        string schemaId,
        ContentSourceRootShape rootShape,
        string? definitionCollectionProperty,
        string reason,
        ContentSourceFamilyActivationPolicy policy,
        string supersedingSource,
        IReadOnlyList<string>? requiredRootProperties = null)
    {
        return ExactConditional(
            familyId,
            sourceId,
            sectionId,
            handleNamespace,
            schemaId,
            rootShape,
            definitionCollectionProperty,
            reason,
            policy,
            new[] { supersedingSource },
            requiredRootProperties);
    }

    private static ContentSourceFamilyDeclaration ExactConditional(
        string familyId,
        string sourceId,
        string sectionId,
        string handleNamespace,
        string schemaId,
        ContentSourceRootShape rootShape,
        string? definitionCollectionProperty,
        string reason,
        ContentSourceFamilyActivationPolicy policy,
        string firstSupersedingSource,
        string secondSupersedingSource)
    {
        return ExactConditional(
            familyId,
            sourceId,
            sectionId,
            handleNamespace,
            schemaId,
            rootShape,
            definitionCollectionProperty,
            reason,
            policy,
            new[] { firstSupersedingSource, secondSupersedingSource },
            requiredRootProperties: null);
    }

    private static ContentSourceFamilyDeclaration ExactConditional(
        string familyId,
        string sourceId,
        string sectionId,
        string handleNamespace,
        string schemaId,
        ContentSourceRootShape rootShape,
        string? definitionCollectionProperty,
        string reason,
        ContentSourceFamilyActivationPolicy policy,
        IReadOnlyList<string> supersedingSources,
        IReadOnlyList<string>? requiredRootProperties)
    {
        return new ContentSourceFamilyDeclaration(
            familyId,
            sourceId,
            ContentSourceFamilyMatchKind.Exact,
            sectionId,
            handleNamespace,
            schemaId,
            rootShape,
            requiredRootProperties ?? Array.Empty<string>(),
            definitionCollectionProperty,
            policy,
            reason,
            supersedingSources);
    }

    private static ContentSourceFamilyDeclaration Tuning(
        string name,
        params string[] requiredRootProperties)
    {
        return ExactActive(
            "content.registry.tuning." + name,
            "content/registries/tuning." + name + ".json",
            "registry.tuning." + name,
            "tuning." + name,
            "source.tuning-" + name + ".schema.json",
            ContentSourceRootShape.Object,
            requiredRootProperties: requiredRootProperties);
    }
}
