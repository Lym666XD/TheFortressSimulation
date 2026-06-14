using LegacyContentRegistry = HumanFortress.Core.Content.ContentRegistry;
using StructuredContentRegistry = HumanFortress.Core.Content.Registry.ContentRegistry;

namespace HumanFortress.Content.Loading;

/// <summary>
/// Coordinates the transitional registry load while legacy and structured registries still coexist.
/// </summary>
public static class RuntimeContentRegistryLoader
{
    public static RuntimeContentRegistryLoadResult Load(
        string contentPath,
        bool continueOnStructuredRegistryError = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);

        var legacyRegistry = LegacyContentRegistry.Instance;
        legacyRegistry.LoadContent(contentPath);

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
            legacyLoaded: legacyRegistry.Materials.Count > 0 || legacyRegistry.GeologyEntries.Count > 0 || legacyRegistry.Zones.Count > 0,
            structuredLoaded,
            legacyRegistry.Materials.Count,
            legacyRegistry.GeologyEntries.Count,
            legacyRegistry.Zones.Count,
            legacyRegistry.Errors.Count,
            structuredRegistry.ValidationResult.Warnings.Count,
            structuredRegistry.ValidationResult.Errors.Count,
            structuredFailure);
    }
}

public sealed class RuntimeContentRegistryLoadResult
{
    public RuntimeContentRegistryLoadResult(
        bool legacyLoaded,
        bool structuredLoaded,
        int legacyMaterialCount,
        int legacyGeologyCount,
        int legacyZoneCount,
        int legacyErrorCount,
        int structuredWarningCount,
        int structuredErrorCount,
        string? structuredFailureMessage)
    {
        LegacyLoaded = legacyLoaded;
        StructuredLoaded = structuredLoaded;
        LegacyMaterialCount = legacyMaterialCount;
        LegacyGeologyCount = legacyGeologyCount;
        LegacyZoneCount = legacyZoneCount;
        LegacyErrorCount = legacyErrorCount;
        StructuredWarningCount = structuredWarningCount;
        StructuredErrorCount = structuredErrorCount;
        StructuredFailureMessage = structuredFailureMessage;
    }

    public bool LegacyLoaded { get; }
    public bool StructuredLoaded { get; }
    public int LegacyMaterialCount { get; }
    public int LegacyGeologyCount { get; }
    public int LegacyZoneCount { get; }
    public int LegacyErrorCount { get; }
    public int StructuredWarningCount { get; }
    public int StructuredErrorCount { get; }
    public string? StructuredFailureMessage { get; }
    public bool HasErrors => LegacyErrorCount > 0 || StructuredErrorCount > 0 || StructuredFailureMessage != null;
}
