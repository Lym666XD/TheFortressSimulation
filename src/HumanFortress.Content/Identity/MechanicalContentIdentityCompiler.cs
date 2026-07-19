using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HumanFortress.Contracts.Content.Identity;

namespace HumanFortress.Content.Identity;

internal static class MechanicalContentIdentityCompiler
{
    internal const int FormatVersion = 2;

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly Regex CanonicalIdPattern = new(
        "^[A-Za-z][A-Za-z0-9_.-]*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    internal static MechanicalContentIdentityData Compile(
        IEnumerable<MechanicalContentSourceDocument> sourceDocuments,
        IEnumerable<ContentSchemaSourceDocument>? schemaDocuments = null,
        IContentJsonSchemaValidationAdapter? schemaAdapter = null,
        bool requireSchemaValidation = false)
    {
        ArgumentNullException.ThrowIfNull(sourceDocuments);

        var sources = sourceDocuments
            .OrderBy(static source => source.SectionId, StringComparer.Ordinal)
            .ThenBy(static source => source.SourceId, StringComparer.Ordinal)
            .ToArray();
        var schemas = schemaDocuments?
            .OrderBy(static schema => schema.SourceId, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<ContentSchemaSourceDocument>();
        var issues = new List<MechanicalContentIssueData>();
        var parsed = new List<ParsedMechanicalContentSource>();
        var excludedSources = new List<string>();

        foreach (var source in sources)
        {
            if (source.IsExcludedFromMechanicalIdentity)
            {
                excludedSources.Add($"{source.SourceId}|{source.ExclusionReason ?? "cosmetic-only"}");
                continue;
            }

            try
            {
                var document = JsonDocument.Parse(source.Json, JsonOptions);
                parsed.Add(new ParsedMechanicalContentSource(
                    source,
                    document,
                    CanonicalMechanicalJsonSerializer.Serialize(
                        document.RootElement,
                        source.FamilyId)));
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                issues.Add(Error(
                    "Content.Syntax.InvalidJson",
                    source.SourceId,
                    "$",
                    exception.Message));
            }
        }

        try
        {
            var handleRows = CompileHandles(parsed, issues);
            MechanicalContentReferenceValidator.Validate(parsed, handleRows, issues);
            var sections = CompileSections(parsed);
            var schemaValidation = ValidateSchemas(
                sources,
                schemas,
                schemaAdapter ?? UnavailableContentJsonSchemaValidationAdapter.Instance,
                requireSchemaValidation);
            if (requireSchemaValidation)
                issues.AddRange(schemaValidation.Issues);

            var orderedIssues = OrderIssues(issues);
            var signature = ComputeOverallSignature(sections, handleRows);
            return new MechanicalContentIdentityData(
                CanonicalMechanicalJsonSerializer.FormatId,
                FormatVersion,
                CanonicalMechanicalJsonSerializer.CosmeticPolicyId,
                signature,
                sections,
                handleRows,
                excludedSources.OrderBy(static source => source, StringComparer.Ordinal).ToArray(),
                orderedIssues,
                schemaValidation);
        }
        finally
        {
            foreach (var source in parsed)
                source.Document.Dispose();
        }
    }

    private static ContentSchemaValidationData ValidateSchemas(
        IReadOnlyList<MechanicalContentSourceDocument> sources,
        IReadOnlyList<ContentSchemaSourceDocument> schemas,
        IContentJsonSchemaValidationAdapter adapter,
        bool required)
    {
        if (required)
            return adapter.Validate(sources, schemas);

        return new ContentSchemaValidationData(
            adapter.AdapterId,
            adapter.Availability,
            Array.Empty<MechanicalContentIssueData>());
    }

    private static MechanicalContentLocalHandleData[] CompileHandles(
        IReadOnlyList<ParsedMechanicalContentSource> sources,
        ICollection<MechanicalContentIssueData> issues)
    {
        var candidates = new List<HandleCandidate>();
        foreach (var source in sources)
        {
            CollectHandleCandidates(
                source.Document.RootElement,
                source.Source.HandleNamespace,
                source.Source.SourceId,
                "$",
                candidates,
                issues,
                isRoot: true);
        }

        var exactGroups = candidates
            .GroupBy(static candidate => candidate.QualifiedId, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToArray();
        foreach (var group in exactGroups.Where(static group => group.Count() > 1))
        {
            var first = group.OrderBy(static candidate => candidate.Source, StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.Path, StringComparer.Ordinal)
                .First();
            issues.Add(Error(
                "Content.Identity.DuplicateId",
                first.Source,
                first.Path,
                $"Canonical id '{group.Key}' is defined {group.Count()} times."));
        }

        foreach (var group in candidates
                     .GroupBy(static candidate => candidate.QualifiedId, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var spellings = group.Select(static candidate => candidate.QualifiedId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray();
            if (spellings.Length <= 1)
                continue;

            var first = group.OrderBy(static candidate => candidate.Source, StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.Path, StringComparer.Ordinal)
                .First();
            issues.Add(Error(
                "Content.Identity.AmbiguousId",
                first.Source,
                first.Path,
                $"Case-insensitive lookup would ambiguously bind: {string.Join(", ", spellings)}."));
        }

        var unique = exactGroups
            .Select(static group => group.First())
            .Where(static candidate => candidate.IsValid)
            .OrderBy(static candidate => candidate.QualifiedId, StringComparer.Ordinal)
            .ToArray();
        if ((ulong)unique.Length >= uint.MaxValue)
        {
            issues.Add(Error(
                "Content.Identity.HandleCapacityExceeded",
                "content",
                "$",
                "Canonical content id count exceeds the uint local-handle capacity."));
            return Array.Empty<MechanicalContentLocalHandleData>();
        }

        var result = new MechanicalContentLocalHandleData[unique.Length];
        for (var index = 0; index < unique.Length; index++)
        {
            var candidate = unique[index];
            result[index] = new MechanicalContentLocalHandleData(
                checked((uint)index + 1u),
                candidate.Namespace,
                candidate.CanonicalId);
        }

        return result;
    }

    private static void CollectHandleCandidates(
        JsonElement element,
        string currentNamespace,
        string source,
        string path,
        ICollection<HandleCandidate> candidates,
        ICollection<MechanicalContentIssueData> issues,
        bool isRoot = false)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in element.EnumerateArray())
            {
                AddHandleCandidate(
                    child,
                    currentNamespace,
                    source,
                    path + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    candidates,
                    issues);
                index++;
            }
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return;

        AddHandleCandidate(element, currentNamespace, source, path, candidates, issues);
        foreach (var property in element.EnumerateObject()
                     .Where(static property => IsRootDefinitionCollection(property.Name))
                     .OrderBy(static property => property.Name, StringComparer.Ordinal))
        {
            var childNamespace = ResolveChildNamespace(currentNamespace, property.Name, isRoot);
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var child in property.Value.EnumerateArray())
                {
                    CollectHandleCandidates(
                        child,
                        childNamespace,
                        source,
                        path + "/" + EscapePointer(property.Name) + "/"
                        + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        candidates,
                        issues);
                    index++;
                }
            }
            else if (property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var child in property.Value.EnumerateObject()
                             .OrderBy(static child => child.Name, StringComparer.Ordinal))
                {
                    CollectHandleCandidates(
                        child.Value,
                        childNamespace,
                        source,
                        path + "/" + EscapePointer(property.Name) + "/"
                        + EscapePointer(child.Name),
                        candidates,
                        issues);
                }
            }
        }
    }

    private static void AddHandleCandidate(
        JsonElement element,
        string currentNamespace,
        string source,
        string path,
        ICollection<HandleCandidate> candidates,
        ICollection<MechanicalContentIssueData> issues)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !TryGetSymbolicId(element, currentNamespace, out var canonicalId))
        {
            return;
        }

        var isValid = !string.IsNullOrWhiteSpace(canonicalId)
            && CanonicalIdPattern.IsMatch(canonicalId);
        if (!isValid)
        {
            issues.Add(Error(
                "Content.Identity.InvalidId",
                source,
                path + "/id",
                $"'{canonicalId}' is not a valid canonical symbolic id."));
        }
        if (!string.IsNullOrWhiteSpace(canonicalId))
        {
            candidates.Add(new HandleCandidate(
                currentNamespace,
                canonicalId,
                source,
                path + "/id",
                isValid));
        }
    }

    private static string ResolveChildNamespace(string currentNamespace, string propertyName, bool isRoot)
    {
        if (propertyName.Equals("attachments", StringComparison.OrdinalIgnoreCase))
            return currentNamespace + ".attachment";
        if (isRoot && IsRootDefinitionCollection(propertyName))
            return currentNamespace;
        return currentNamespace;
    }

    private static bool TryGetSymbolicId(
        JsonElement element,
        string currentNamespace,
        out string canonicalId)
    {
        if (element.TryGetProperty("id", out var idElement)
            && idElement.ValueKind == JsonValueKind.String)
        {
            canonicalId = idElement.GetString() ?? string.Empty;
            return true;
        }

        // Terrain's persisted numeric bit id is a local representation. Its
        // stable symbolic identity is the ordinal name.
        if (currentNamespace.Equals("terrain", StringComparison.Ordinal)
            && element.TryGetProperty("name", out var nameElement)
            && nameElement.ValueKind == JsonValueKind.String)
        {
            canonicalId = nameElement.GetString() ?? string.Empty;
            return true;
        }

        canonicalId = string.Empty;
        return false;
    }

    private static bool IsRootDefinitionCollection(string propertyName)
    {
        return propertyName is "items" or "creatures" or "constructions" or "workshops"
            or "recipes" or "materials" or "prototypes" or "templates" or "zones"
            or "professions" or "presets" or "terrainKinds" or "body_plans"
            or "orders" or "attachments" or "designations" or "tools";
    }

    private static MechanicalContentSectionIdentityData[] CompileSections(
        IReadOnlyList<ParsedMechanicalContentSource> sources)
    {
        return sources
            .GroupBy(static source => source.Source.SectionId, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var documents = group.Select(static source => source.CanonicalBytes).ToArray();
                Array.Sort(documents, CanonicalBytesComparer.Instance);
                return new MechanicalContentSectionIdentityData(
                    group.Key,
                    documents.Length,
                    ComputeSectionHash(group.Key, documents));
            })
            .ToArray();
    }

    private static string ComputeSectionHash(string sectionId, IReadOnlyList<byte[]> documents)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, CanonicalMechanicalJsonSerializer.FormatId);
        AppendString(hash, sectionId);
        AppendInt32(hash, documents.Count);
        foreach (var document in documents)
        {
            AppendInt32(hash, document.Length);
            hash.AppendData(document);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string ComputeOverallSignature(
        IReadOnlyList<MechanicalContentSectionIdentityData> sections,
        IReadOnlyList<MechanicalContentLocalHandleData> handles)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, CanonicalMechanicalJsonSerializer.FormatId);
        AppendInt32(hash, FormatVersion);
        AppendString(hash, CanonicalMechanicalJsonSerializer.CosmeticPolicyId);
        AppendInt32(hash, sections.Count);
        foreach (var section in sections)
        {
            AppendString(hash, section.SectionId);
            AppendInt32(hash, section.DocumentCount);
            AppendString(hash, section.MechanicalHash);
        }

        AppendInt32(hash, handles.Count);
        foreach (var handle in handles)
        {
            AppendUInt32(hash, handle.Handle);
            AppendString(hash, handle.Namespace);
            AppendString(hash, handle.CanonicalId);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static MechanicalContentIssueData[] OrderIssues(IEnumerable<MechanicalContentIssueData> issues)
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

    private static void AppendString(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendUInt32(IncrementalHash hash, uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static string EscapePointer(string value)
    {
        return value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);
    }

    internal sealed record ParsedMechanicalContentSource(
        MechanicalContentSourceDocument Source,
        JsonDocument Document,
        byte[] CanonicalBytes);

    private sealed record HandleCandidate(
        string Namespace,
        string CanonicalId,
        string Source,
        string Path,
        bool IsValid)
    {
        internal string QualifiedId => $"{Namespace}:{CanonicalId}";
    }

    private sealed class CanonicalBytesComparer : IComparer<byte[]>
    {
        internal static CanonicalBytesComparer Instance { get; } = new();

        int IComparer<byte[]>.Compare(byte[]? left, byte[]? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            return left.AsSpan().SequenceCompareTo(right);
        }
    }
}
