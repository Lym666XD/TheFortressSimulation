using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSlotMigrationPlanBuilder
{
    internal static RuntimeSaveSlotMigrationPlanData Build(
        RuntimeSaveSnapshotDocumentValidationResultData validation,
        RuntimeSaveSlotCompatibilityData compatibility,
        RuntimeSaveSlotManifestData? manifest)
    {
        if (!compatibility.RequiresMigration)
        {
            return new RuntimeSaveSlotMigrationPlanData(
                RequiresMigration: false,
                CanMigrate: false,
                SourceSlotFormatVersion: compatibility.SlotFormatVersion,
                TargetSlotFormatVersion: RuntimeSaveSlotFormat.CurrentVersion,
                SourceRuntimeSnapshotFormatVersion: compatibility.RuntimeSnapshotFormatVersion,
                TargetRuntimeSnapshotFormatVersion: RuntimeSaveFormat.CurrentVersion,
                RequiredTransforms: Array.Empty<string>(),
                BlockingIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }

        var requiredTransforms = RuntimeSaveSlotMigrationTransformRegistry.BuildRequiredTransformIds(
            compatibility,
            manifest);
        var transformPathAvailable = RuntimeSaveSlotMigrationTransformRegistry.CanSatisfy(
            requiredTransforms,
            out var missingTransforms);
        var blockingIssues = new List<RuntimeSaveSnapshotDocumentIssueData>();
        if (!validation.Success)
        {
            blockingIssues.AddRange(validation.Issues.Where(static issue =>
                !string.Equals(issue.Section, "slot.compatibility", StringComparison.Ordinal)));
        }

        blockingIssues.RemoveAll(issue => IsExpectedSlotTransformInputIssue(
            issue,
            compatibility,
            manifest));

        if (!transformPathAvailable)
        {
            blockingIssues.Add(new RuntimeSaveSnapshotDocumentIssueData(
                "slot.migration",
                null,
                RuntimeSaveSlotMigrationTransformRegistry.CreateMissingTransformsMessage(
                    requiredTransforms,
                    missingTransforms)));
        }

        var canMigrate = transformPathAvailable && blockingIssues.Count == 0;

        return new RuntimeSaveSlotMigrationPlanData(
            RequiresMigration: true,
            CanMigrate: canMigrate,
            SourceSlotFormatVersion: compatibility.SlotFormatVersion,
            TargetSlotFormatVersion: RuntimeSaveSlotFormat.CurrentVersion,
            SourceRuntimeSnapshotFormatVersion: compatibility.RuntimeSnapshotFormatVersion,
            TargetRuntimeSnapshotFormatVersion: RuntimeSaveFormat.CurrentVersion,
            RequiredTransforms: requiredTransforms,
            BlockingIssues: blockingIssues.ToArray());
    }

    private static bool IsExpectedSlotTransformInputIssue(
        RuntimeSaveSnapshotDocumentIssueData issue,
        RuntimeSaveSlotCompatibilityData compatibility,
        RuntimeSaveSlotManifestData? manifest)
    {
        if (!string.Equals(issue.Section, "slot.manifest", StringComparison.Ordinal))
        {
            return IsExpectedRuntimeSnapshotTransformInputIssue(
                issue,
                compatibility);
        }

        if (!compatibility.RequiresMigration
            || compatibility.SlotFormatVersion >= RuntimeSaveSlotFormat.CurrentVersion)
        {
            return false;
        }

        if (!manifest.HasValue)
            return issue.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);

        return string.Equals(
            issue.Message,
            "Unsupported save slot manifest format version.",
            StringComparison.Ordinal);
    }

    private static bool IsExpectedRuntimeSnapshotTransformInputIssue(
        RuntimeSaveSnapshotDocumentIssueData issue,
        RuntimeSaveSlotCompatibilityData compatibility)
    {
        if (!string.Equals(issue.Section, "manifest", StringComparison.Ordinal))
            return false;

        return compatibility.RequiresMigration
            && compatibility.RuntimeSnapshotFormatVersion < RuntimeSaveFormat.CurrentVersion
            && issue.Message.Contains("Unsupported save format version", StringComparison.Ordinal);
    }
}
