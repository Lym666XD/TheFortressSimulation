namespace HumanFortress.Contracts.Content.Identity;

public enum MechanicalContentIssueSeverity : byte
{
    Warning = 0,
    Error = 1,
}

public sealed record MechanicalContentIssueData(
    MechanicalContentIssueSeverity Severity,
    string Code,
    string Source,
    string Path,
    string Message);

public sealed record MechanicalContentSectionIdentityData(
    string SectionId,
    int DocumentCount,
    string MechanicalHash);

public sealed record MechanicalContentLocalHandleData(
    uint Handle,
    string Namespace,
    string CanonicalId)
{
    public string QualifiedId => $"{Namespace}:{CanonicalId}";
}

public enum ContentSchemaValidatorAvailability : byte
{
    Unavailable = 0,
    Available = 1,
}

public sealed class ContentSchemaValidationData
{
    public ContentSchemaValidationData(
        string adapterId,
        ContentSchemaValidatorAvailability availability,
        IReadOnlyList<MechanicalContentIssueData> issues)
    {
        AdapterId = adapterId ?? throw new ArgumentNullException(nameof(adapterId));
        Availability = availability;
        Issues = Array.AsReadOnly(issues?.ToArray() ?? throw new ArgumentNullException(nameof(issues)));
    }

    public string AdapterId { get; }

    public ContentSchemaValidatorAvailability Availability { get; }

    public IReadOnlyList<MechanicalContentIssueData> Issues { get; }

    public bool IsValid => Availability == ContentSchemaValidatorAvailability.Available
        && !Issues.Any(static issue => issue.Severity == MechanicalContentIssueSeverity.Error);
}

/// <summary>
/// Reproducible identity of every compiled mechanical content source. Handles
/// are local accelerators; CanonicalId remains the authoritative diagnostic key.
/// </summary>
public sealed class MechanicalContentIdentityData
{
    private readonly IReadOnlyDictionary<string, uint> _handlesByQualifiedId;
    private readonly IReadOnlyDictionary<uint, MechanicalContentLocalHandleData> _rowsByHandle;

    public MechanicalContentIdentityData(
        string formatId,
        int formatVersion,
        string cosmeticPolicyId,
        string mechanicalSignature,
        IReadOnlyList<MechanicalContentSectionIdentityData> sections,
        IReadOnlyList<MechanicalContentLocalHandleData> localHandles,
        IReadOnlyList<string> excludedSources,
        IReadOnlyList<MechanicalContentIssueData> issues,
        ContentSchemaValidationData schemaValidation)
    {
        FormatId = formatId ?? throw new ArgumentNullException(nameof(formatId));
        FormatVersion = formatVersion;
        CosmeticPolicyId = cosmeticPolicyId ?? throw new ArgumentNullException(nameof(cosmeticPolicyId));
        MechanicalSignature = mechanicalSignature ?? throw new ArgumentNullException(nameof(mechanicalSignature));
        Sections = Array.AsReadOnly(sections?.ToArray() ?? throw new ArgumentNullException(nameof(sections)));
        LocalHandles = Array.AsReadOnly(localHandles?.ToArray() ?? throw new ArgumentNullException(nameof(localHandles)));
        ExcludedSources = Array.AsReadOnly(
            excludedSources?.ToArray() ?? throw new ArgumentNullException(nameof(excludedSources)));
        Issues = Array.AsReadOnly(issues?.ToArray() ?? throw new ArgumentNullException(nameof(issues)));
        SchemaValidation = schemaValidation ?? throw new ArgumentNullException(nameof(schemaValidation));

        _handlesByQualifiedId = LocalHandles.ToDictionary(
            static row => row.QualifiedId,
            static row => row.Handle,
            StringComparer.Ordinal);
        _rowsByHandle = LocalHandles.ToDictionary(static row => row.Handle);
    }

    public string FormatId { get; }

    public int FormatVersion { get; }

    public string CosmeticPolicyId { get; }

    public string MechanicalSignature { get; }

    public IReadOnlyList<MechanicalContentSectionIdentityData> Sections { get; }

    public IReadOnlyList<MechanicalContentLocalHandleData> LocalHandles { get; }

    public IReadOnlyList<string> ExcludedSources { get; }

    public IReadOnlyList<MechanicalContentIssueData> Issues { get; }

    public ContentSchemaValidationData SchemaValidation { get; }

    public bool HasMechanicalErrors => Issues.Any(
        static issue => issue.Severity == MechanicalContentIssueSeverity.Error);

    public bool TryGetLocalHandle(string @namespace, string canonicalId, out uint handle)
    {
        handle = 0;
        if (string.IsNullOrWhiteSpace(@namespace) || string.IsNullOrWhiteSpace(canonicalId))
            return false;
        return _handlesByQualifiedId.TryGetValue($"{@namespace}:{canonicalId}", out handle);
    }

    public bool TryResolveLocalHandle(uint handle, out MechanicalContentLocalHandleData? row)
    {
        if (_rowsByHandle.TryGetValue(handle, out var resolved))
        {
            row = resolved;
            return true;
        }

        row = null;
        return false;
    }
}
