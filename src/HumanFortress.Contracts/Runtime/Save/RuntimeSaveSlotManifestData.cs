using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Contracts.Runtime.Save;

public static class RuntimeSaveSlotFormat
{
    public const int CurrentVersion = 1;
    public const string SlotKind = "runtime-snapshot-slot";
    public const string ManifestFileName = "slot_manifest.json";
    public const string SnapshotDocumentFileName = "runtime_snapshot.json";
}

public readonly record struct RuntimeSaveSlotManifestData(
    int SlotFormatVersion,
    string SlotKind,
    string RuntimeSnapshotDocumentFileName,
    int RuntimeSnapshotFormatVersion,
    string EngineBuild,
    SimulationSnapshotMetadata Metadata,
    string CheckpointAggregateHash,
    string? WorldHash,
    int ManifestSectionCount,
    int RngStreamCount,
    int ExecutedCommandRecordCount,
    int PendingCommandRecordCount);

public enum RuntimeSaveSlotCompatibilityStatus
{
    Unavailable = 0,
    Compatible = 1,
    MigrationRequired = 2,
    UnsupportedSlotKind = 3,
    UnsupportedSnapshotDocument = 4,
    UnsupportedFutureSlotFormat = 5,
    UnsupportedFutureRuntimeSnapshotFormat = 6
}

public readonly record struct RuntimeSaveSlotCompatibilityData(
    RuntimeSaveSlotCompatibilityStatus Status,
    bool CanRead,
    bool RequiresMigration,
    int CurrentSlotFormatVersion,
    int SlotFormatVersion,
    int CurrentRuntimeSnapshotFormatVersion,
    int RuntimeSnapshotFormatVersion,
    string Message)
{
    public static RuntimeSaveSlotCompatibilityData Unavailable { get; } = new(
        Status: RuntimeSaveSlotCompatibilityStatus.Unavailable,
        CanRead: false,
        RequiresMigration: false,
        CurrentSlotFormatVersion: RuntimeSaveSlotFormat.CurrentVersion,
        SlotFormatVersion: 0,
        CurrentRuntimeSnapshotFormatVersion: RuntimeSaveFormat.CurrentVersion,
        RuntimeSnapshotFormatVersion: 0,
        Message: "Save slot compatibility could not be evaluated because the slot manifest was not readable.");
}

public readonly record struct RuntimeSaveSlotInspectionData(
    bool Success,
    RuntimeSaveSnapshotDocumentValidationResultData Validation,
    RuntimeSaveSlotCompatibilityData Compatibility,
    RuntimeSaveSlotContentCompatibilityData ContentCompatibility,
    RuntimeSaveSlotMigrationPlanData MigrationPlan,
    RuntimeSaveSlotRestorePlanData RestorePlan,
    RuntimeSaveSlotManifestData? Manifest);

public enum RuntimeSaveSlotContentCompatibilityStatus
{
    Unavailable = 0,
    Compatible = 1,
    MissingSavedContentSignature = 2,
    CurrentContentUnavailable = 3,
    ContentVersionMismatch = 4,
    ContentHashMismatch = 5,
    MaterialContentHashMismatch = 6,
    CatalogShapeMismatch = 7
}

public readonly record struct RuntimeSaveSlotContentCompatibilityData(
    RuntimeSaveSlotContentCompatibilityStatus Status,
    bool CanBindContent,
    bool RequiresMissingContentPolicy,
    RuntimeSaveContentSignatureData SavedContent,
    RuntimeSaveContentSignatureData CurrentContent,
    RuntimeSaveContentCatalogSummaryData SavedCatalog,
    RuntimeSaveContentCatalogSummaryData CurrentCatalog,
    RuntimeSaveContentCompatibilityDifferenceData[] DifferenceDetails,
    string[] Differences,
    RuntimeSaveSnapshotDocumentIssueData[] BlockingIssues)
{
    public static RuntimeSaveSlotContentCompatibilityData Unavailable { get; } = new(
        Status: RuntimeSaveSlotContentCompatibilityStatus.Unavailable,
        CanBindContent: false,
        RequiresMissingContentPolicy: true,
        SavedContent: RuntimeSaveContentSignatureData.Unavailable,
        CurrentContent: RuntimeSaveContentSignatureData.Unavailable,
        SavedCatalog: RuntimeSaveContentCatalogSummaryData.Unavailable,
        CurrentCatalog: RuntimeSaveContentCatalogSummaryData.Unavailable,
        DifferenceDetails: Array.Empty<RuntimeSaveContentCompatibilityDifferenceData>(),
        Differences: Array.Empty<string>(),
        BlockingIssues: new[]
        {
            new RuntimeSaveSnapshotDocumentIssueData(
                "slot.content",
                null,
                "Save slot content compatibility could not be evaluated.")
        });
}

public readonly record struct RuntimeSaveContentCatalogSummaryData(
    bool HasCatalog,
    string[] MaterialNames,
    string[] TerrainKindNames,
    string[] ConstructionIds,
    string[] RecipeIds,
    string[] GeologyIds,
    string[] ZoneIds)
{
    public static RuntimeSaveContentCatalogSummaryData Unavailable { get; } = new(
        HasCatalog: false,
        MaterialNames: Array.Empty<string>(),
        TerrainKindNames: Array.Empty<string>(),
        ConstructionIds: Array.Empty<string>(),
        RecipeIds: Array.Empty<string>(),
        GeologyIds: Array.Empty<string>(),
        ZoneIds: Array.Empty<string>());
}

public enum RuntimeSaveContentCompatibilityDifferenceKind
{
    Unknown = 0,
    ContentVersion = 1,
    ContentHash = 2,
    MaterialContentHash = 3,
    MaterialCount = 4,
    TerrainKindCount = 5,
    ConstructionCount = 6,
    RecipeCount = 7,
    GeologyCount = 8,
    ZoneCount = 9
}

public readonly record struct RuntimeSaveContentCompatibilityDifferenceData(
    RuntimeSaveContentCompatibilityDifferenceKind Kind,
    string Field,
    string SavedValue,
    string CurrentValue,
    bool HasSavedCatalogKeys,
    bool HasCurrentCatalogKeys,
    string[] MissingCurrentKeys,
    string[] AdditionalCurrentKeys,
    string Message);

public readonly record struct RuntimeSaveSlotMigrationPlanData(
    bool RequiresMigration,
    bool CanMigrate,
    int SourceSlotFormatVersion,
    int TargetSlotFormatVersion,
    int SourceRuntimeSnapshotFormatVersion,
    int TargetRuntimeSnapshotFormatVersion,
    string[] RequiredTransforms,
    RuntimeSaveSnapshotDocumentIssueData[] BlockingIssues);

public readonly record struct RuntimeSaveSlotMigrationResultData(
    bool Success,
    bool MigrationApplied,
    RuntimeSaveSlotInspectionData Inspection,
    string SourceDirectory,
    string TargetDirectory,
    string[] AppliedTransforms,
    RuntimeSaveSnapshotDocumentIssueData[] MigrationIssues);

public readonly record struct RuntimeSaveSlotRestorePlanData(
    bool CanRestorePendingCommands,
    bool CanRestoreWorld,
    bool CanRestoreFull,
    RuntimeSaveSnapshotDocumentIssueData[] BlockingIssues);
