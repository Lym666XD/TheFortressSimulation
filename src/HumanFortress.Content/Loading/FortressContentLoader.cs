using HumanFortress.Content.Definitions;
using LegacyContentRegistry = HumanFortress.Core.Content.ContentRegistry;
using StructuredContentRegistry = HumanFortress.Core.Content.Registry.ContentRegistry;

namespace HumanFortress.Content.Loading;

/// <summary>
/// Resolved content directory candidates for either published output or a source checkout.
/// </summary>
public sealed class ContentPathResolution
{
    public ContentPathResolution(string publishedPath, string developmentPath, string? resolvedPath)
    {
        PublishedPath = publishedPath ?? throw new ArgumentNullException(nameof(publishedPath));
        DevelopmentPath = developmentPath ?? throw new ArgumentNullException(nameof(developmentPath));
        ResolvedPath = resolvedPath;
    }

    public string PublishedPath { get; }
    public string DevelopmentPath { get; }
    public string? ResolvedPath { get; }
    public bool Found => ResolvedPath != null;
}

/// <summary>
/// Resolved content file candidates for either published output or a source checkout.
/// </summary>
public sealed class ContentFileResolution
{
    public ContentFileResolution(string publishedPath, string developmentPath, string? resolvedPath)
    {
        PublishedPath = publishedPath ?? throw new ArgumentNullException(nameof(publishedPath));
        DevelopmentPath = developmentPath ?? throw new ArgumentNullException(nameof(developmentPath));
        ResolvedPath = resolvedPath;
    }

    public string PublishedPath { get; }
    public string DevelopmentPath { get; }
    public string? ResolvedPath { get; }
    public bool Found => ResolvedPath != null;
}

/// <summary>
/// Aggregated runtime content bootstrap result for a fortress session.
/// </summary>
public sealed class FortressContentLoadResult
{
    public FortressContentLoadResult(
        ContentPathResolution contentPath,
        ContentPathResolution coreDataPath,
        RuntimeContentRegistryLoadResult? registries,
        CoreContentCatalogLoadResult? coreCatalogs,
        bool registriesAlreadyLoaded,
        IReadOnlyList<FortressContentIssue> issues)
    {
        ContentPath = contentPath ?? throw new ArgumentNullException(nameof(contentPath));
        CoreDataPath = coreDataPath ?? throw new ArgumentNullException(nameof(coreDataPath));
        Registries = registries;
        CoreCatalogs = coreCatalogs;
        RegistriesAlreadyLoaded = registriesAlreadyLoaded;
        Issues = issues?.ToArray() ?? throw new ArgumentNullException(nameof(issues));
    }

    public ContentPathResolution ContentPath { get; }
    public ContentPathResolution CoreDataPath { get; }
    public RuntimeContentRegistryLoadResult? Registries { get; }
    public CoreContentCatalogLoadResult? CoreCatalogs { get; }
    public bool RegistriesAlreadyLoaded { get; }
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
}

/// <summary>
/// Single Content-owned entry point for locating and loading runtime content.
/// </summary>
public static class FortressContentLoader
{
    public static FortressContentLoadResult Load(
        string baseDir,
        bool includeRegistries = true,
        bool includeCoreCatalogs = true,
        bool forceReloadRegistries = false,
        bool continueOnStructuredRegistryError = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        var contentPath = ResolveContentPath(baseDir);
        var coreDataPath = ResolveCoreDataPath(baseDir);
        RuntimeContentRegistryLoadResult? registries = null;
        var registriesAlreadyLoaded = false;
        var issues = new List<FortressContentIssue>();

        if (includeRegistries && contentPath.ResolvedPath != null)
        {
            registriesAlreadyLoaded = !forceReloadRegistries && AreRegistriesLoaded();
            if (!registriesAlreadyLoaded)
            {
                registries = RuntimeContentRegistryLoader.Load(
                    contentPath.ResolvedPath,
                    continueOnStructuredRegistryError);
            }
        }

        CoreContentCatalogLoadResult? coreCatalogs = null;
        if (includeCoreCatalogs && coreDataPath.ResolvedPath != null)
        {
            coreCatalogs = CoreContentCatalogLoader.Load(coreDataPath.ResolvedPath);
        }

        AddRegistryIssues(includeRegistries, contentPath, registries, registriesAlreadyLoaded, issues);
        AddCoreCatalogIssues(includeCoreCatalogs, coreDataPath, coreCatalogs, issues);

        return new FortressContentLoadResult(
            contentPath,
            coreDataPath,
            registries,
            coreCatalogs,
            registriesAlreadyLoaded,
            issues);
    }

    public static ContentPathResolution ResolveContentPath(string baseDir)
    {
        return ResolveChildDirectory(baseDir, "content");
    }

    public static ContentPathResolution ResolveCoreDataPath(string baseDir)
    {
        return ResolveChildDirectory(baseDir, Path.Combine("data", "core"));
    }

    public static ContentFileResolution ResolveRegistryFile(string baseDir, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var contentPath = ResolveContentPath(baseDir);
        var publishedPath = Path.Combine(contentPath.PublishedPath, "registries", fileName);
        var developmentPath = Path.Combine(contentPath.DevelopmentPath, "registries", fileName);

        if (File.Exists(publishedPath))
        {
            return new ContentFileResolution(publishedPath, developmentPath, publishedPath);
        }

        return new ContentFileResolution(
            publishedPath,
            developmentPath,
            File.Exists(developmentPath) ? developmentPath : null);
    }

    private static ContentPathResolution ResolveChildDirectory(string baseDir, string relativePath)
    {
        var publishedPath = Path.Combine(baseDir, relativePath);
        var developmentPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", relativePath));

        if (Directory.Exists(publishedPath))
        {
            return new ContentPathResolution(publishedPath, developmentPath, publishedPath);
        }

        return new ContentPathResolution(
            publishedPath,
            developmentPath,
            Directory.Exists(developmentPath) ? developmentPath : null);
    }

    private static bool AreRegistriesLoaded()
    {
        return LegacyContentRegistry.Instance.Materials.Count > 0
            && StructuredContentRegistry.Instance.IsLoaded;
    }

    private static void AddRegistryIssues(
        bool includeRegistries,
        ContentPathResolution contentPath,
        RuntimeContentRegistryLoadResult? registries,
        bool registriesAlreadyLoaded,
        List<FortressContentIssue> issues)
    {
        if (!includeRegistries)
        {
            return;
        }

        if (contentPath.ResolvedPath == null)
        {
            issues.Add(Error(
                "Content.PathMissing",
                $"Content directory not found. Tried: {contentPath.PublishedPath}; {contentPath.DevelopmentPath}"));
            return;
        }

        if (registriesAlreadyLoaded)
        {
            return;
        }

        if (registries == null)
        {
            issues.Add(Error(
                "Content.RegistriesNotLoaded",
                $"Content registries were not loaded from {contentPath.ResolvedPath}."));
            return;
        }

        if (!registries.LegacyLoaded)
        {
            issues.Add(Error(
                "Content.LegacyRegistryEmpty",
                $"Legacy content registry loaded no materials/geology/zones from {contentPath.ResolvedPath}."));
        }

        if (registries.LegacyErrorCount > 0)
        {
            issues.Add(Error(
                "Content.LegacyRegistryErrors",
                $"Legacy content registry reported {registries.LegacyErrorCount} error(s)."));
        }

        if (!registries.StructuredLoaded)
        {
            issues.Add(Error(
                "Content.StructuredRegistryUnavailable",
                registries.StructuredFailureMessage ?? "Structured content registry did not load."));
        }

        if (registries.StructuredErrorCount > 0)
        {
            issues.Add(Error(
                "Content.StructuredRegistryErrors",
                $"Structured content registry reported {registries.StructuredErrorCount} error(s)."));
        }

        if (registries.StructuredWarningCount > 0)
        {
            issues.Add(Warning(
                "Content.StructuredRegistryWarnings",
                $"Structured content registry reported {registries.StructuredWarningCount} warning(s)."));
        }
    }

    private static void AddCoreCatalogIssues(
        bool includeCoreCatalogs,
        ContentPathResolution coreDataPath,
        CoreContentCatalogLoadResult? coreCatalogs,
        List<FortressContentIssue> issues)
    {
        if (!includeCoreCatalogs)
        {
            return;
        }

        if (coreDataPath.ResolvedPath == null)
        {
            issues.Add(Error(
                "Content.CoreDataPathMissing",
                $"Core data directory not found. Tried: {coreDataPath.PublishedPath}; {coreDataPath.DevelopmentPath}"));
            return;
        }

        if (coreCatalogs == null)
        {
            issues.Add(Error(
                "Content.CoreCatalogsNotLoaded",
                $"Core content catalogs were not loaded from {coreDataPath.ResolvedPath}."));
            return;
        }

        AddCountIssue(
            coreCatalogs.Items.LoadedCount,
            coreCatalogs.Items.ErrorCount,
            "Content.Items",
            "item definition",
            issues);
        AddCountIssue(
            coreCatalogs.Creatures.LoadedCount,
            coreCatalogs.Creatures.ErrorCount,
            "Content.Creatures",
            "creature definition",
            issues);
        AddCountIssue(
            coreCatalogs.Constructions.LoadedCount,
            coreCatalogs.Constructions.ErrorCount,
            "Content.Constructions",
            "construction definition",
            issues);
        AddCountIssue(
            coreCatalogs.Recipes.LoadedCount,
            coreCatalogs.Recipes.ErrorCount,
            "Content.Recipes",
            "recipe definition",
            issues);
    }

    private static void AddCountIssue(
        int loadedCount,
        int errorCount,
        string codePrefix,
        string noun,
        List<FortressContentIssue> issues)
    {
        if (loadedCount <= 0)
        {
            issues.Add(Error(
                $"{codePrefix}Empty",
                $"Loaded 0 {noun}s."));
        }

        if (errorCount > 0)
        {
            issues.Add(Error(
                $"{codePrefix}Errors",
                $"Encountered {errorCount} {noun} error(s)."));
        }
    }

    private static FortressContentIssue Error(string code, string message)
    {
        return new FortressContentIssue(FortressContentIssueSeverity.Error, code, message);
    }

    private static FortressContentIssue Warning(string code, string message)
    {
        return new FortressContentIssue(FortressContentIssueSeverity.Warning, code, message);
    }
}
