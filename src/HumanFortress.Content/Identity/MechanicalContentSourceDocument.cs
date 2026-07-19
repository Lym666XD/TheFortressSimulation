namespace HumanFortress.Content.Identity;

internal sealed record MechanicalContentSourceDocument(
    string SectionId,
    string SourceId,
    string HandleNamespace,
    string Json,
    bool IsExcludedFromMechanicalIdentity = false,
    string? ExclusionReason = null,
    string FamilyId = "synthetic",
    string? SchemaId = null,
    bool RequiresSourceFamilyManifest = false);
