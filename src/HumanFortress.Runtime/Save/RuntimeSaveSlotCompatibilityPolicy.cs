using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSlotCompatibilityPolicy
{
    internal static RuntimeSaveSlotCompatibilityData EvaluateLegacySnapshotDocument(
        RuntimeSaveSnapshotDocumentData document)
    {
        if (document.Manifest.FormatVersion > RuntimeSaveFormat.CurrentVersion)
        {
            return Rejected(
                RuntimeSaveSlotCompatibilityStatus.UnsupportedFutureRuntimeSnapshotFormat,
                slotFormatVersion: 0,
                runtimeSnapshotFormatVersion: document.Manifest.FormatVersion,
                "Legacy Runtime snapshot document format is newer than this runtime can read.");
        }

        return MigrationRequired(
            slotFormatVersion: 0,
            runtimeSnapshotFormatVersion: document.Manifest.FormatVersion,
            "Legacy Runtime snapshot directory is missing a save slot manifest; Runtime migration must create one before load.");
    }

    internal static RuntimeSaveSlotCompatibilityData Evaluate(RuntimeSaveSlotManifestData manifest)
    {
        if (!string.Equals(manifest.SlotKind, RuntimeSaveSlotFormat.SlotKind, StringComparison.Ordinal))
        {
            return Rejected(
                RuntimeSaveSlotCompatibilityStatus.UnsupportedSlotKind,
                manifest,
                "Save slot kind is not recognized by this runtime.");
        }

        if (!string.Equals(manifest.RuntimeSnapshotDocumentFileName, RuntimeSaveSlotFormat.SnapshotDocumentFileName, StringComparison.Ordinal))
        {
            return Rejected(
                RuntimeSaveSlotCompatibilityStatus.UnsupportedSnapshotDocument,
                manifest,
                "Save slot points at an unsupported Runtime snapshot document.");
        }

        if (manifest.SlotFormatVersion < RuntimeSaveSlotFormat.CurrentVersion)
        {
            return MigrationRequired(
                manifest,
                "Save slot format is older than this runtime supports directly; a migration table must handle it before load.");
        }

        if (manifest.SlotFormatVersion > RuntimeSaveSlotFormat.CurrentVersion)
        {
            return Rejected(
                RuntimeSaveSlotCompatibilityStatus.UnsupportedFutureSlotFormat,
                manifest,
                "Save slot format is newer than this runtime can read.");
        }

        if (manifest.RuntimeSnapshotFormatVersion < RuntimeSaveFormat.CurrentVersion)
        {
            return MigrationRequired(
                manifest,
                "Runtime snapshot format is older than this runtime supports directly; a migration table must handle it before load.");
        }

        if (manifest.RuntimeSnapshotFormatVersion > RuntimeSaveFormat.CurrentVersion)
        {
            return Rejected(
                RuntimeSaveSlotCompatibilityStatus.UnsupportedFutureRuntimeSnapshotFormat,
                manifest,
                "Runtime snapshot format is newer than this runtime can read.");
        }

        return new RuntimeSaveSlotCompatibilityData(
            RuntimeSaveSlotCompatibilityStatus.Compatible,
            CanRead: true,
            RequiresMigration: false,
            RuntimeSaveSlotFormat.CurrentVersion,
            manifest.SlotFormatVersion,
            RuntimeSaveFormat.CurrentVersion,
            manifest.RuntimeSnapshotFormatVersion,
            "Save slot is compatible with the current Runtime save reader.");
    }

    internal static RuntimeSaveSnapshotDocumentIssueData? ToValidationIssue(
        RuntimeSaveSlotCompatibilityData compatibility)
    {
        if (compatibility.CanRead)
            return null;

        return new RuntimeSaveSnapshotDocumentIssueData(
            "slot.compatibility",
            null,
            compatibility.Message);
    }

    private static RuntimeSaveSlotCompatibilityData MigrationRequired(
        RuntimeSaveSlotManifestData manifest,
        string message)
    {
        return MigrationRequired(
            manifest.SlotFormatVersion,
            manifest.RuntimeSnapshotFormatVersion,
            message);
    }

    private static RuntimeSaveSlotCompatibilityData MigrationRequired(
        int slotFormatVersion,
        int runtimeSnapshotFormatVersion,
        string message)
    {
        return new RuntimeSaveSlotCompatibilityData(
            RuntimeSaveSlotCompatibilityStatus.MigrationRequired,
            CanRead: false,
            RequiresMigration: true,
            RuntimeSaveSlotFormat.CurrentVersion,
            slotFormatVersion,
            RuntimeSaveFormat.CurrentVersion,
            runtimeSnapshotFormatVersion,
            message);
    }

    private static RuntimeSaveSlotCompatibilityData Rejected(
        RuntimeSaveSlotCompatibilityStatus status,
        RuntimeSaveSlotManifestData manifest,
        string message)
    {
        return Rejected(
            status,
            manifest.SlotFormatVersion,
            manifest.RuntimeSnapshotFormatVersion,
            message);
    }

    private static RuntimeSaveSlotCompatibilityData Rejected(
        RuntimeSaveSlotCompatibilityStatus status,
        int slotFormatVersion,
        int runtimeSnapshotFormatVersion,
        string message)
    {
        return new RuntimeSaveSlotCompatibilityData(
            status,
            CanRead: false,
            RequiresMigration: false,
            RuntimeSaveSlotFormat.CurrentVersion,
            slotFormatVersion,
            RuntimeSaveFormat.CurrentVersion,
            runtimeSnapshotFormatVersion,
            message);
    }
}
