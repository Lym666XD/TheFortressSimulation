using System.Globalization;
using System.Text.Json;
using HumanFortress.Contracts.Content.Identity;
using Json.Schema;

namespace HumanFortress.Content.Identity;

internal interface IContentJsonSchemaValidationAdapter
{
    string AdapterId { get; }

    ContentSchemaValidatorAvailability Availability { get; }

    ContentSchemaValidationData Validate(
        IReadOnlyList<MechanicalContentSourceDocument> sources,
        IReadOnlyList<ContentSchemaSourceDocument> schemas);
}

/// <summary>
/// Standards-compliant, local-only JSON Schema validation. Every schema and
/// reference is resolved from the supplied content snapshot before evaluation.
/// </summary>
internal sealed class JsonSchemaNetContentValidationAdapter : IContentJsonSchemaValidationAdapter
{
    internal const string StableAdapterId = "JsonSchema.Net/9.2.2:local-only";

    private static readonly Uri SchemaBaseUri = new("https://content.humanfortress.invalid/");

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly HashSet<string> SupportedDialectUris = new(StringComparer.Ordinal)
    {
        "http://json-schema.org/draft-07/schema#",
        "https://json-schema.org/draft/2019-09/schema",
        "https://json-schema.org/draft/2020-12/schema"
    };

    internal static JsonSchemaNetContentValidationAdapter Instance { get; } = new();

    private JsonSchemaNetContentValidationAdapter()
    {
    }

    string IContentJsonSchemaValidationAdapter.AdapterId => StableAdapterId;

    ContentSchemaValidatorAvailability IContentJsonSchemaValidationAdapter.Availability =>
        ContentSchemaValidatorAvailability.Available;

    ContentSchemaValidationData IContentJsonSchemaValidationAdapter.Validate(
        IReadOnlyList<MechanicalContentSourceDocument> sources,
        IReadOnlyList<ContentSchemaSourceDocument> schemas)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(schemas);

        var issues = new List<MechanicalContentIssueData>();
        var definitions = ParseSchemaDefinitions(schemas, issues);
        var aliases = BuildSchemaAliases(definitions, issues);
        PreflightSchemaReferences(definitions, aliases, issues);
        BuildSchemas(definitions, issues);
        ValidateSources(sources, aliases, issues);

        return new ContentSchemaValidationData(
            StableAdapterId,
            ContentSchemaValidatorAvailability.Available,
            OrderIssues(issues));
    }

    private static SchemaDefinition[] ParseSchemaDefinitions(
        IEnumerable<ContentSchemaSourceDocument> schemas,
        ICollection<MechanicalContentIssueData> issues)
    {
        var definitions = new List<SchemaDefinition>();
        foreach (var schema in schemas.OrderBy(static value => value.SourceId, StringComparer.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(schema.Json, JsonOptions);
                if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.True or JsonValueKind.False))
                {
                    issues.Add(Error(
                        "Content.Schema.Invalid",
                        schema.SourceId,
                        "$",
                        "A JSON Schema document must be an object or a boolean."));
                    continue;
                }

                var root = document.RootElement.Clone();
                if (!HasSupportedDialect(root, out var dialectError))
                {
                    issues.Add(Error(
                        "Content.Schema.UnsupportedDialect",
                        schema.SourceId,
                        "$/$schema",
                        dialectError));
                    continue;
                }

                definitions.Add(new SchemaDefinition(
                    schema,
                    CreateDocumentUri(schema.SourceId),
                    root));
            }
            catch (JsonException exception)
            {
                issues.Add(Error(
                    "Content.Schema.InvalidJson",
                    schema.SourceId,
                    "$",
                    exception.Message));
            }
        }

        return definitions.ToArray();
    }

    private static bool HasSupportedDialect(JsonElement schema, out string error)
    {
        error = string.Empty;
        if (schema.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("$schema", out var dialect))
        {
            return true;
        }

        if (dialect.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(dialect.GetString()))
        {
            error = "The $schema keyword must contain a non-empty absolute URI.";
            return false;
        }

        var value = dialect.GetString()!;
        if (!SupportedDialectUris.Contains(value))
        {
            error = $"Schema dialect '{value}' is not in the pinned local dialect registry.";
            return false;
        }

        return true;
    }

    private static Dictionary<string, SchemaDefinition?> BuildSchemaAliases(
        IReadOnlyList<SchemaDefinition> definitions,
        ICollection<MechanicalContentIssueData> issues)
    {
        var aliases = new Dictionary<string, SchemaDefinition?>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            AddAlias(aliases, definition, definition.Source.SourceId, issues);
            AddAlias(aliases, definition, NormalizePath(definition.Source.SourceId), issues);
            AddAlias(aliases, definition, Path.GetFileName(definition.Source.SourceId), issues);
            AddAlias(aliases, definition, definition.DocumentUri.AbsoluteUri, issues);

            if (definition.Root.ValueKind == JsonValueKind.Object
                && definition.Root.TryGetProperty("$id", out var idElement)
                && idElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(idElement.GetString()))
            {
                var id = idElement.GetString()!;
                AddAlias(aliases, definition, id, issues);
                if (Uri.TryCreate(definition.DocumentUri, id, out var resolvedId))
                    AddAlias(aliases, definition, WithoutFragment(resolvedId).AbsoluteUri, issues);
            }
        }

        return aliases;
    }

    private static void AddAlias(
        IDictionary<string, SchemaDefinition?> aliases,
        SchemaDefinition definition,
        string alias,
        ICollection<MechanicalContentIssueData> issues)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return;

        var normalized = NormalizeReference(alias);
        if (!aliases.TryGetValue(normalized, out var existing))
        {
            aliases[normalized] = definition;
            return;
        }

        if (existing == null || ReferenceEquals(existing, definition))
            return;

        aliases[normalized] = null;
        issues.Add(Error(
            "Content.Schema.AmbiguousAlias",
            definition.Source.SourceId,
            "$",
            $"Schema alias '{normalized}' is also provided by '{existing.Source.SourceId}'."));
    }

    private static void PreflightSchemaReferences(
        IReadOnlyList<SchemaDefinition> definitions,
        IReadOnlyDictionary<string, SchemaDefinition?> aliases,
        ICollection<MechanicalContentIssueData> issues)
    {
        foreach (var definition in definitions)
        {
            foreach (var reference in EnumerateExternalReferences(definition.Root, "$"))
            {
                if (TryResolveSchema(
                        reference.Value,
                        definition.ReferenceBaseUri,
                        aliases,
                        out var dependency,
                        out var resolvedUri))
                {
                    definition.Dependencies.Add(new SchemaDependency(dependency!, resolvedUri!));
                    continue;
                }

                definition.CanBuild = false;
                issues.Add(Error(
                    "Content.Schema.ReferenceUnresolved",
                    definition.Source.SourceId,
                    reference.Path,
                    $"External schema reference '{reference.Value}' is not present in the local schema registry."));
            }
        }
    }

    private static IEnumerable<SchemaReference> EnumerateExternalReferences(
        JsonElement element,
        string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject().OrderBy(static value => value.Name, StringComparer.Ordinal))
            {
                var propertyPath = path + "/" + EscapePointer(property.Name);
                if (property.Name is "$ref" or "$dynamicRef" or "$recursiveRef"
                    && property.Value.ValueKind == JsonValueKind.String
                    && property.Value.GetString() is { Length: > 0 } reference
                    && !reference.StartsWith('#'))
                {
                    yield return new SchemaReference(reference, propertyPath);
                }

                foreach (var nested in EnumerateExternalReferences(property.Value, propertyPath))
                    yield return nested;
            }

            yield break;
        }

        if (element.ValueKind != JsonValueKind.Array)
            yield break;

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            foreach (var nested in EnumerateExternalReferences(
                         item,
                         path + "/" + index.ToString(CultureInfo.InvariantCulture)))
            {
                yield return nested;
            }

            index++;
        }
    }

    private static void BuildSchemas(
        IReadOnlyList<SchemaDefinition> definitions,
        ICollection<MechanicalContentIssueData> issues)
    {
        var registry = new SchemaRegistry
        {
            Fetch = static (_, _) => null
        };
        var buildOptions = new BuildOptions
        {
            SchemaRegistry = registry,
            Dialect = Dialect.Draft07
        };

        var states = new Dictionary<SchemaDefinition, SchemaBuildState>();
        foreach (var definition in definitions)
            BuildSchema(definition, registry, buildOptions, states, issues);
    }

    private static void BuildSchema(
        SchemaDefinition definition,
        SchemaRegistry registry,
        BuildOptions buildOptions,
        IDictionary<SchemaDefinition, SchemaBuildState> states,
        ICollection<MechanicalContentIssueData> issues)
    {
        if (!definition.CanBuild)
            return;

        if (states.TryGetValue(definition, out var state))
        {
            if (state == SchemaBuildState.Built)
                return;

            definition.CanBuild = false;
            issues.Add(Error(
                "Content.Schema.DependencyCycle",
                definition.Source.SourceId,
                "$",
                "External schema dependency cycles are not supported by the local-only build gate."));
            return;
        }

        states[definition] = SchemaBuildState.Building;
        foreach (var dependency in definition.Dependencies
                     .OrderBy(static value => value.Definition.Source.SourceId, StringComparer.Ordinal)
                     .ThenBy(static value => value.ResolutionUri.AbsoluteUri, StringComparer.Ordinal))
        {
            BuildSchema(dependency.Definition, registry, buildOptions, states, issues);
            if (dependency.Definition.Compiled == null || !dependency.Definition.CanBuild)
            {
                definition.CanBuild = false;
                issues.Add(Error(
                    "Content.Schema.DependencyInvalid",
                    definition.Source.SourceId,
                    "$",
                    $"Referenced schema '{dependency.Definition.Source.SourceId}' is invalid."));
                continue;
            }

            try
            {
                registry.Register(dependency.ResolutionUri, dependency.Definition.Compiled);
            }
            catch (JsonSchemaException exception)
            {
                definition.CanBuild = false;
                issues.Add(Error(
                    "Content.Schema.ReferenceCollision",
                    definition.Source.SourceId,
                    "$",
                    exception.Message));
            }
        }

        if (!definition.CanBuild)
            return;

        try
        {
            definition.Compiled = JsonSchema.Build(
                definition.Root,
                buildOptions,
                definition.DocumentUri);
            registry.Register(definition.DocumentUri, definition.Compiled);
            states[definition] = SchemaBuildState.Built;
        }
        catch (Exception exception) when (exception is JsonException
                                           or JsonSchemaException
                                           or ArgumentException
                                           or InvalidOperationException)
        {
            definition.CanBuild = false;
            issues.Add(Error(
                "Content.Schema.Invalid",
                definition.Source.SourceId,
                "$",
                exception.Message));
        }
    }

    private static void ValidateSources(
        IEnumerable<MechanicalContentSourceDocument> sources,
        IReadOnlyDictionary<string, SchemaDefinition?> aliases,
        ICollection<MechanicalContentIssueData> issues)
    {
        var evaluationOptions = new EvaluationOptions
        {
            Culture = CultureInfo.InvariantCulture,
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true
        };

        foreach (var source in sources
                     .Where(static value => !value.IsExcludedFromMechanicalIdentity)
                     .OrderBy(static value => value.SourceId, StringComparer.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(source.Json, JsonOptions);
                if (!ValidateFamilyContract(source, document.RootElement, issues))
                    continue;

                if (!TryResolveSourceSchema(source, document.RootElement, aliases, out var definition, out var error))
                {
                    issues.Add(Error(error.Code, source.SourceId, error.Path, error.Message));
                    continue;
                }

                if (definition?.Compiled == null || !definition.CanBuild)
                {
                    issues.Add(Error(
                        "Content.Schema.Unavailable",
                        source.SourceId,
                        "$/$schema",
                        $"Mapped schema '{definition?.Source.SourceId ?? "<ambiguous>"}' is not valid and available."));
                    continue;
                }

                var result = definition.Compiled.Evaluate(document.RootElement, evaluationOptions);
                if (result.IsValid)
                    continue;

                var issueCount = AddEvaluationIssues(source.SourceId, result, issues);
                if (issueCount == 0)
                {
                    issues.Add(Error(
                        "Content.Schema.ValidationFailed",
                        source.SourceId,
                        "$",
                        $"Document did not satisfy '{definition.Source.SourceId}'."));
                }
            }
            catch (JsonException exception)
            {
                issues.Add(Error(
                    "Content.Schema.SourceInvalidJson",
                    source.SourceId,
                    "$",
                    exception.Message));
            }
            catch (Exception exception) when (exception is JsonSchemaException
                                               or ArgumentException
                                               or InvalidOperationException)
            {
                issues.Add(Error(
                    "Content.Schema.EvaluationFailed",
                    source.SourceId,
                    "$",
                    exception.Message));
            }
        }
    }

    private static bool TryResolveSourceSchema(
        MechanicalContentSourceDocument source,
        JsonElement root,
        IReadOnlyDictionary<string, SchemaDefinition?> aliases,
        out SchemaDefinition? definition,
        out ResolutionError error)
    {
        definition = null;
        error = default;

        if (source.RequiresSourceFamilyManifest)
        {
            if (source.FamilyId.Equals("unclassified", StringComparison.Ordinal)
                || !MechanicalContentSourceFamilyManifest.TryGetDeclaration(
                    source.FamilyId,
                    out var declaration))
            {
                error = new ResolutionError(
                    "Content.Schema.SourceUnclassified",
                    "$",
                    $"Active source is not classified by '{MechanicalContentSourceFamilyManifest.CanonicalPolicyId}'.");
                return false;
            }

            if (!MechanicalContentSourceFamilyManifest.TryResolve(
                    source.SourceId,
                    new HashSet<string>(StringComparer.Ordinal) { source.SourceId },
                    out var resolvedFamily)
                || !resolvedFamily.FamilyId.Equals(source.FamilyId, StringComparison.Ordinal))
            {
                error = new ResolutionError(
                    "Content.Schema.SourceFamilyMismatch",
                    "$",
                    $"Source '{source.SourceId}' does not belong to declared family '{source.FamilyId}'.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(source.SchemaId)
                || !string.Equals(source.SchemaId, declaration.SchemaId, StringComparison.Ordinal))
            {
                error = new ResolutionError(
                    "Content.Schema.FamilyContractInvalid",
                    "$",
                    $"Source family '{source.FamilyId}' does not carry its declared schema contract.");
                return false;
            }

            if (TryResolveSchemaAlias(source.SchemaId, aliases, out definition))
                return true;

            error = new ResolutionError(
                "Content.Schema.FamilyResolutionFailed",
                "$",
                $"Source family '{source.FamilyId}' maps to '{source.SchemaId}', but that schema is unavailable.");
            return false;
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("$schema", out var schemaReference))
        {
            if (schemaReference.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(schemaReference.GetString()))
            {
                error = new ResolutionError(
                    "Content.Schema.ExplicitResolutionFailed",
                    "$/$schema",
                    "The explicit $schema value must be a non-empty string.");
                return false;
            }

            var value = schemaReference.GetString()!;
            if (TryResolveSchema(value, CreateDocumentUri(source.SourceId), aliases, out definition))
                return true;

            error = new ResolutionError(
                "Content.Schema.ExplicitResolutionFailed",
                "$/$schema",
                $"Explicit schema reference '{value}' is not present in the local schema registry.");
            return false;
        }

        error = new ResolutionError(
            "Content.Schema.SourceUnsupported",
            "$",
            "No explicit $schema or deterministic source-family schema mapping is defined.");
        return false;
    }

    private static bool ValidateFamilyContract(
        MechanicalContentSourceDocument source,
        JsonElement root,
        ICollection<MechanicalContentIssueData> issues)
    {
        if (!source.RequiresSourceFamilyManifest
            || source.FamilyId.Equals("unclassified", StringComparison.Ordinal))
        {
            return true;
        }

        if (!MechanicalContentSourceFamilyManifest.TryGetDeclaration(
                source.FamilyId,
                out var declaration))
        {
            return true;
        }

        var valid = true;
        if (!MatchesRootShape(root.ValueKind, declaration.RootShape))
        {
            issues.Add(Error(
                "Content.Schema.FamilyRootMismatch",
                source.SourceId,
                "$",
                $"Source family '{source.FamilyId}' requires root shape '{declaration.RootShape}'."));
            return false;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in declaration.RequiredRootProperties
                         .OrderBy(static value => value, StringComparer.Ordinal))
            {
                if (root.TryGetProperty(property, out _))
                    continue;

                issues.Add(Error(
                    "Content.Schema.FamilyRequiredPropertyMissing",
                    source.SourceId,
                    "$",
                    $"Source family '{source.FamilyId}' requires root property '{property}'."));
                valid = false;
            }
        }

        if (declaration.DefinitionCollectionProperty == null)
        {
            if (root.ValueKind == JsonValueKind.Array)
                valid &= ValidateDefinitionArray(source, root, "$", issues);
            return valid;
        }

        if (root.ValueKind == JsonValueKind.Array)
            return ValidateDefinitionArray(source, root, "$", issues) && valid;

        if (!root.TryGetProperty(declaration.DefinitionCollectionProperty, out var collection))
            return valid;

        if (collection.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error(
                "Content.Schema.FamilyDefinitionCollectionInvalid",
                source.SourceId,
                "$/" + EscapePointer(declaration.DefinitionCollectionProperty),
                $"Definition collection '{declaration.DefinitionCollectionProperty}' must be an array."));
            return false;
        }

        return ValidateDefinitionArray(
            source,
            collection,
            "$/" + EscapePointer(declaration.DefinitionCollectionProperty),
            issues) && valid;
    }

    private static bool ValidateDefinitionArray(
        MechanicalContentSourceDocument source,
        JsonElement array,
        string path,
        ICollection<MechanicalContentIssueData> issues)
    {
        var valid = true;
        var index = 0;
        foreach (var definition in array.EnumerateArray())
        {
            if (definition.ValueKind != JsonValueKind.Object
                || !definition.TryGetProperty("id", out var id)
                || id.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(id.GetString()))
            {
                issues.Add(Error(
                    "Content.Schema.FamilyDefinitionIdMissing",
                    source.SourceId,
                    path + "/" + index.ToString(CultureInfo.InvariantCulture),
                    $"Definitions in source family '{source.FamilyId}' require a non-empty string id."));
                valid = false;
            }

            index++;
        }

        return valid;
    }

    private static bool MatchesRootShape(
        JsonValueKind valueKind,
        ContentSourceRootShape rootShape)
    {
        return rootShape switch
        {
            ContentSourceRootShape.Object => valueKind == JsonValueKind.Object,
            ContentSourceRootShape.Array => valueKind == JsonValueKind.Array,
            ContentSourceRootShape.ObjectOrArray =>
                valueKind is JsonValueKind.Object or JsonValueKind.Array,
            _ => false,
        };
    }

    private static bool TryResolveSchema(
        string reference,
        Uri documentUri,
        IReadOnlyDictionary<string, SchemaDefinition?> aliases,
        out SchemaDefinition? definition)
    {
        return TryResolveSchema(reference, documentUri, aliases, out definition, out _);
    }

    private static bool TryResolveSchema(
        string reference,
        Uri documentUri,
        IReadOnlyDictionary<string, SchemaDefinition?> aliases,
        out SchemaDefinition? definition,
        out Uri? resolvedUri)
    {
        definition = null;
        resolvedUri = null;
        if (!Uri.TryCreate(documentUri, reference, out var resolved))
            return false;

        resolvedUri = WithoutFragment(resolved);
        var normalized = NormalizeReference(resolvedUri.AbsoluteUri);
        if (aliases.TryGetValue(normalized, out definition))
            return definition != null;

        definition = null;
        resolvedUri = null;
        return false;
    }

    private static bool TryResolveSchemaAlias(
        string alias,
        IReadOnlyDictionary<string, SchemaDefinition?> aliases,
        out SchemaDefinition? definition)
    {
        return aliases.TryGetValue(NormalizeReference(alias), out definition)
            && definition != null;
    }

    private static int AddEvaluationIssues(
        string source,
        EvaluationResults result,
        ICollection<MechanicalContentIssueData> issues)
    {
        var added = 0;
        var queue = new Queue<EvaluationResults>();
        queue.Enqueue(result);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Errors != null)
            {
                foreach (var error in current.Errors.OrderBy(static value => value.Key, StringComparer.Ordinal)
                             .ThenBy(static value => value.Value, StringComparer.Ordinal))
                {
                    var pointer = current.InstanceLocation.ToString();
                    issues.Add(Error(
                        "Content.Schema.ValidationFailed",
                        source,
                        string.IsNullOrEmpty(pointer) ? "$" : "$" + pointer,
                        $"{error.Key}: {error.Value}"));
                    added++;
                }
            }

            if (current.Details == null)
                continue;

            foreach (var detail in current.Details)
                queue.Enqueue(detail);
        }

        return added;
    }

    private static Uri CreateDocumentUri(string sourceId)
    {
        var normalized = NormalizePath(sourceId).TrimStart('/');
        return new Uri(SchemaBaseUri, normalized);
    }

    private static Uri WithoutFragment(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.Fragment))
            return uri;

        return new UriBuilder(uri) { Fragment = string.Empty }.Uri;
    }

    private static string NormalizeReference(string reference)
    {
        return NormalizePath(StripFragment(reference));
    }

    private static string NormalizePath(string value)
    {
        return value.Replace('\\', '/');
    }

    private static string StripFragment(string reference)
    {
        var fragmentIndex = reference.IndexOf('#', StringComparison.Ordinal);
        return fragmentIndex < 0 ? reference : reference[..fragmentIndex];
    }

    private static string EscapePointer(string value)
    {
        return value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);
    }

    private static MechanicalContentIssueData[] OrderIssues(
        IEnumerable<MechanicalContentIssueData> issues)
    {
        return issues
            .OrderBy(static issue => issue.Source, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Path, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Message, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Severity)
            .ToArray();
    }

    private static MechanicalContentIssueData Error(
        string code,
        string source,
        string path,
        string message)
    {
        return new MechanicalContentIssueData(
            MechanicalContentIssueSeverity.Error,
            code,
            source,
            path,
            message);
    }

    private sealed class SchemaDefinition(
        ContentSchemaSourceDocument source,
        Uri documentUri,
        JsonElement root)
    {
        internal ContentSchemaSourceDocument Source { get; } = source;
        internal Uri DocumentUri { get; } = documentUri;
        internal Uri ReferenceBaseUri { get; } = ResolveReferenceBaseUri(documentUri, root);
        internal JsonElement Root { get; } = root;
        internal bool CanBuild { get; set; } = true;
        internal JsonSchema? Compiled { get; set; }
        internal List<SchemaDependency> Dependencies { get; } = new();
    }

    private static Uri ResolveReferenceBaseUri(Uri documentUri, JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("$id", out var id)
            && id.ValueKind == JsonValueKind.String
            && Uri.TryCreate(documentUri, id.GetString(), out var resolved))
        {
            return WithoutFragment(resolved);
        }

        return documentUri;
    }

    private readonly record struct SchemaReference(string Value, string Path);

    private readonly record struct SchemaDependency(SchemaDefinition Definition, Uri ResolutionUri);

    private readonly record struct ResolutionError(string Code, string Path, string Message);

    private enum SchemaBuildState : byte
    {
        Building = 0,
        Built = 1
    }
}

internal sealed class UnavailableContentJsonSchemaValidationAdapter : IContentJsonSchemaValidationAdapter
{
    internal static UnavailableContentJsonSchemaValidationAdapter Instance { get; } = new();

    private UnavailableContentJsonSchemaValidationAdapter()
    {
    }

    string IContentJsonSchemaValidationAdapter.AdapterId =>
        "unavailable:requires-pinned-json-schema-validator";

    ContentSchemaValidatorAvailability IContentJsonSchemaValidationAdapter.Availability =>
        ContentSchemaValidatorAvailability.Unavailable;

    ContentSchemaValidationData IContentJsonSchemaValidationAdapter.Validate(
        IReadOnlyList<MechanicalContentSourceDocument> sources,
        IReadOnlyList<ContentSchemaSourceDocument> schemas)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(schemas);

        return new ContentSchemaValidationData(
            ((IContentJsonSchemaValidationAdapter)this).AdapterId,
            ((IContentJsonSchemaValidationAdapter)this).Availability,
            new[]
            {
                new MechanicalContentIssueData(
                    MechanicalContentIssueSeverity.Error,
                    "Content.Schema.ValidatorUnavailable",
                    "content/schemas",
                    "$",
                    "Strict schema validation requires the pinned local JSON Schema adapter.")
            });
    }
}

internal sealed record ContentSchemaSourceDocument(string SourceId, string Json);
