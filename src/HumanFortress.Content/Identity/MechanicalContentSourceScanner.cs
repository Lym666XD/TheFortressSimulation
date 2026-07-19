namespace HumanFortress.Content.Identity;

internal sealed record MechanicalContentSourceSet(
    IReadOnlyList<MechanicalContentSourceDocument> MechanicalSources,
    IReadOnlyList<ContentSchemaSourceDocument> Schemas);

internal static class MechanicalContentSourceScanner
{
    internal static MechanicalContentSourceSet Scan(string contentPath, string coreDataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(coreDataPath);

        var schemas = ScanSchemas(contentPath);
        var candidates = ScanCandidates(contentPath, coreDataPath)
            .OrderBy(static candidate => candidate.SourceId, StringComparer.Ordinal)
            .ToArray();
        var availableSourceIds = candidates
            .Select(static candidate => candidate.SourceId)
            .ToHashSet(StringComparer.Ordinal);
        var sources = candidates
            .Select(candidate => CreateSource(candidate, availableSourceIds))
            .OrderBy(static source => source.SectionId, StringComparer.Ordinal)
            .ThenBy(static source => source.SourceId, StringComparer.Ordinal)
            .ToArray();

        return new MechanicalContentSourceSet(sources, schemas);
    }

    private static ContentSchemaSourceDocument[] ScanSchemas(string contentPath)
    {
        var schemasPath = Path.Combine(contentPath, "schemas");
        if (!Directory.Exists(schemasPath))
            return Array.Empty<ContentSchemaSourceDocument>();

        return EnumerateJsonFiles(schemasPath)
            .Select(file => new ContentSchemaSourceDocument(
                ToContentSourceId(contentPath, file),
                File.ReadAllText(file)))
            .OrderBy(static schema => schema.SourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<SourceCandidate> ScanCandidates(
        string contentPath,
        string coreDataPath)
    {
        var registriesPath = Path.Combine(contentPath, "registries");
        if (Directory.Exists(registriesPath))
        {
            foreach (var file in EnumerateJsonFiles(registriesPath))
                yield return new SourceCandidate(ToContentSourceId(contentPath, file), file);
        }

        var templatesPath = Path.Combine(contentPath, "templates");
        if (Directory.Exists(templatesPath))
        {
            foreach (var file in EnumerateJsonFiles(templatesPath))
                yield return new SourceCandidate(ToContentSourceId(contentPath, file), file);
        }

        if (!Directory.Exists(coreDataPath))
            yield break;

        foreach (var file in EnumerateJsonFiles(coreDataPath))
        {
            var relative = Path.GetRelativePath(coreDataPath, file).Replace('\\', '/');
            yield return new SourceCandidate("data/core/" + relative, file);
        }
    }

    private static MechanicalContentSourceDocument CreateSource(
        SourceCandidate candidate,
        IReadOnlySet<string> availableSourceIds)
    {
        if (!MechanicalContentSourceFamilyManifest.TryResolve(
                candidate.SourceId,
                availableSourceIds,
                out var resolution))
        {
            return new MechanicalContentSourceDocument(
                SectionId: "unclassified",
                SourceId: candidate.SourceId,
                HandleNamespace: "unclassified",
                Json: File.ReadAllText(candidate.Path),
                FamilyId: "unclassified",
                RequiresSourceFamilyManifest: true);
        }

        return new MechanicalContentSourceDocument(
            resolution.SectionId,
            candidate.SourceId,
            resolution.HandleNamespace,
            File.ReadAllText(candidate.Path),
            IsExcludedFromMechanicalIdentity: !resolution.IsActive,
            ExclusionReason: resolution.ExclusionReason,
            FamilyId: resolution.FamilyId,
            SchemaId: resolution.SchemaId,
            RequiresSourceFamilyManifest: true);
    }

    private static IEnumerable<string> EnumerateJsonFiles(string root)
    {
        return Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
            .OrderBy(static file => file, StringComparer.Ordinal);
    }

    private static string ToContentSourceId(string contentPath, string file)
    {
        return "content/" + Path.GetRelativePath(contentPath, file).Replace('\\', '/');
    }

    private sealed record SourceCandidate(string SourceId, string Path);
}
