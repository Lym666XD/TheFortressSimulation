namespace HumanFortress.Contracts.Content.Loading;

/// <summary>
/// App/Runtime-facing summary of a content load without exposing Content-owned catalog packages.
/// </summary>
public sealed class FortressContentLoadReport
{
    public FortressContentLoadReport(
        ContentPathResolution contentPath,
        ContentPathResolution coreDataPath,
        bool registriesAlreadyLoaded,
        bool structuredRegistriesLoaded,
        int structuredRegistryWarningCount,
        int structuredRegistryErrorCount,
        string? structuredRegistryFailureMessage,
        bool coreCatalogsLoaded,
        int itemDefinitionLoadedCount,
        int itemDefinitionErrorCount,
        int creatureDefinitionLoadedCount,
        int creatureDefinitionErrorCount,
        int constructionDefinitionLoadedCount,
        int constructionDefinitionErrorCount,
        int recipeDefinitionLoadedCount,
        int recipeDefinitionErrorCount,
        IReadOnlyList<FortressContentIssue> issues)
    {
        ContentPath = contentPath ?? throw new ArgumentNullException(nameof(contentPath));
        CoreDataPath = coreDataPath ?? throw new ArgumentNullException(nameof(coreDataPath));
        RegistriesAlreadyLoaded = registriesAlreadyLoaded;
        StructuredRegistriesLoaded = structuredRegistriesLoaded;
        StructuredRegistryWarningCount = structuredRegistryWarningCount;
        StructuredRegistryErrorCount = structuredRegistryErrorCount;
        StructuredRegistryFailureMessage = structuredRegistryFailureMessage;
        CoreCatalogsLoaded = coreCatalogsLoaded;
        ItemDefinitionLoadedCount = itemDefinitionLoadedCount;
        ItemDefinitionErrorCount = itemDefinitionErrorCount;
        CreatureDefinitionLoadedCount = creatureDefinitionLoadedCount;
        CreatureDefinitionErrorCount = creatureDefinitionErrorCount;
        ConstructionDefinitionLoadedCount = constructionDefinitionLoadedCount;
        ConstructionDefinitionErrorCount = constructionDefinitionErrorCount;
        RecipeDefinitionLoadedCount = recipeDefinitionLoadedCount;
        RecipeDefinitionErrorCount = recipeDefinitionErrorCount;
        Issues = issues?.ToArray() ?? throw new ArgumentNullException(nameof(issues));
    }

    public ContentPathResolution ContentPath { get; }

    public ContentPathResolution CoreDataPath { get; }

    public bool RegistriesAlreadyLoaded { get; }

    public bool StructuredRegistriesLoaded { get; }

    public int StructuredRegistryWarningCount { get; }

    public int StructuredRegistryErrorCount { get; }

    public string? StructuredRegistryFailureMessage { get; }

    public bool CoreCatalogsLoaded { get; }

    public int ItemDefinitionLoadedCount { get; }

    public int ItemDefinitionErrorCount { get; }

    public int CreatureDefinitionLoadedCount { get; }

    public int CreatureDefinitionErrorCount { get; }

    public int ConstructionDefinitionLoadedCount { get; }

    public int ConstructionDefinitionErrorCount { get; }

    public int RecipeDefinitionLoadedCount { get; }

    public int RecipeDefinitionErrorCount { get; }

    public IReadOnlyList<FortressContentIssue> Issues { get; }

    public bool HasErrors => Issues.Any(issue => issue.Severity == FortressContentIssueSeverity.Error);

    public bool HasWarnings => Issues.Any(issue => issue.Severity == FortressContentIssueSeverity.Warning);

    public bool IsValid(bool treatWarningsAsErrors = false)
    {
        return GetBlockingIssues(treatWarningsAsErrors).Count == 0;
    }

    public IReadOnlyList<FortressContentIssue> GetBlockingIssues(bool treatWarningsAsErrors = false)
    {
        return Issues
            .Where(issue =>
                issue.Severity == FortressContentIssueSeverity.Error
                || (treatWarningsAsErrors && issue.Severity == FortressContentIssueSeverity.Warning))
            .ToArray();
    }

    public string FormatBlockingIssues(bool treatWarningsAsErrors = false)
    {
        return string.Join(Environment.NewLine, GetBlockingIssues(treatWarningsAsErrors));
    }

    public void ThrowIfInvalid(bool treatWarningsAsErrors = false)
    {
        var blockingIssues = GetBlockingIssues(treatWarningsAsErrors);
        if (blockingIssues.Count > 0)
        {
            throw new FortressContentLoadException(blockingIssues);
        }
    }
}
