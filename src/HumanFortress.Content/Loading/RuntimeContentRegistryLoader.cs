using StructuredContentRegistry = HumanFortress.Content.Registry.ContentRegistry;

namespace HumanFortress.Content.Loading;

/// <summary>
/// Coordinates the runtime structured registry load.
/// </summary>
internal static class RuntimeContentRegistryLoader
{
    internal static RuntimeContentRegistryLoadResult Load(
        string contentPath,
        bool continueOnStructuredRegistryError = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);

        var structuredRegistry = StructuredContentRegistry.Instance;
        var structuredLoaded = false;
        string? structuredFailure = null;

        try
        {
            structuredRegistry.LoadContent(contentPath);
            structuredLoaded = true;
        }
        catch (Exception ex) when (continueOnStructuredRegistryError)
        {
            structuredFailure = ex.Message;
        }

        return new RuntimeContentRegistryLoadResult(
            structuredLoaded,
            structuredRegistry.ValidationResult.Warnings.Count,
            structuredRegistry.ValidationResult.Errors.Count,
            structuredFailure);
    }
}

internal sealed class RuntimeContentRegistryLoadResult
{
    internal RuntimeContentRegistryLoadResult(
        bool structuredLoaded,
        int structuredWarningCount,
        int structuredErrorCount,
        string? structuredFailureMessage)
    {
        StructuredLoaded = structuredLoaded;
        StructuredWarningCount = structuredWarningCount;
        StructuredErrorCount = structuredErrorCount;
        StructuredFailureMessage = structuredFailureMessage;
    }

    internal bool StructuredLoaded { get; }
    internal int StructuredWarningCount { get; }
    internal int StructuredErrorCount { get; }
    internal string? StructuredFailureMessage { get; }
    internal bool HasErrors => StructuredErrorCount > 0 || StructuredFailureMessage != null;
}
