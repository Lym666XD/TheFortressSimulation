using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSlotMigrator
{
    internal static RuntimeSaveSlotMigrationResultData MigrateDirectory(
        string sourceDirectory,
        string targetDirectory)
    {
        var inspection = RuntimeSaveSnapshotDocumentStore.InspectDirectory(sourceDirectory);
        if (!inspection.MigrationPlan.RequiresMigration)
        {
            var issues = BuildNoMigrationIssues(inspection);
            return new RuntimeSaveSlotMigrationResultData(
                Success: issues.Length == 0,
                MigrationApplied: false,
                Inspection: inspection,
                SourceDirectory: sourceDirectory,
                TargetDirectory: targetDirectory,
                AppliedTransforms: Array.Empty<string>(),
                MigrationIssues: issues);
        }

        if (!inspection.MigrationPlan.CanMigrate)
        {
            return new RuntimeSaveSlotMigrationResultData(
                Success: false,
                MigrationApplied: false,
                Inspection: inspection,
                SourceDirectory: sourceDirectory,
                TargetDirectory: targetDirectory,
                AppliedTransforms: Array.Empty<string>(),
                MigrationIssues: BuildBlockedMigrationIssues(inspection));
        }

        return RuntimeSaveSlotMigrationTransformRegistry.ApplyTransforms(
            sourceDirectory,
            targetDirectory,
            inspection);
    }

    private static RuntimeSaveSnapshotDocumentIssueData[] BuildNoMigrationIssues(
        RuntimeSaveSlotInspectionData inspection)
    {
        if (inspection.Success && inspection.Compatibility.CanRead)
            return Array.Empty<RuntimeSaveSnapshotDocumentIssueData>();

        if (inspection.Validation.Issues.Length > 0)
            return inspection.Validation.Issues;

        return new[]
        {
            new RuntimeSaveSnapshotDocumentIssueData(
                "slot.migration",
                null,
                "Save slot does not require migration, but it is not readable by this runtime.")
        };
    }

    private static RuntimeSaveSnapshotDocumentIssueData[] BuildBlockedMigrationIssues(
        RuntimeSaveSlotInspectionData inspection)
    {
        if (inspection.MigrationPlan.BlockingIssues.Length > 0)
            return inspection.MigrationPlan.BlockingIssues;

        if (inspection.Validation.Issues.Length > 0)
            return inspection.Validation.Issues;

        return new[]
        {
            new RuntimeSaveSnapshotDocumentIssueData(
                "slot.migration",
                null,
                "Save slot migration is required, but no Runtime migration path can apply it.")
        };
    }
}
