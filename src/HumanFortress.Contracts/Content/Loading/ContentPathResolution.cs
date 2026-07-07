namespace HumanFortress.Contracts.Content.Loading;

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
