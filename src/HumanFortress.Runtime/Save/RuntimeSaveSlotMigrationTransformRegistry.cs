using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSlotMigrationTransformRegistry
{
    private const string LegacySnapshotOnlySlotTransformId = "slot:0->1";
    private const string RuntimeSnapshot4To5TransformId = "runtime_snapshot:4->5";
    private const string RuntimeSnapshot5To6TransformId = "runtime_snapshot:5->6";

    private static readonly string[] RegisteredTransformIds =
    {
        LegacySnapshotOnlySlotTransformId,
        RuntimeSnapshot4To5TransformId,
        RuntimeSnapshot5To6TransformId
    };

    internal static string[] BuildRequiredTransformIds(
        RuntimeSaveSlotCompatibilityData compatibility,
        RuntimeSaveSlotManifestData? manifest)
    {
        var transforms = new List<string>();

        for (var version = compatibility.RuntimeSnapshotFormatVersion;
             version < RuntimeSaveFormat.CurrentVersion;
             version++)
        {
            transforms.Add(
                CreateRuntimeSnapshotTransformId(
                    version,
                    version + 1));
        }

        if (compatibility.SlotFormatVersion < RuntimeSaveSlotFormat.CurrentVersion)
        {
            transforms.Add(
                CreateSlotTransformId(
                    compatibility.SlotFormatVersion,
                    RuntimeSaveSlotFormat.CurrentVersion));
        }

        return transforms.ToArray();
    }

    internal static bool CanSatisfy(
        IReadOnlyList<string> requiredTransformIds,
        out string[] missingTransformIds)
    {
        var registered = RegisteredTransformIds.ToHashSet(StringComparer.Ordinal);
        missingTransformIds = requiredTransformIds
            .Where(required => !registered.Contains(required))
            .OrderBy(static required => required, StringComparer.Ordinal)
            .ToArray();

        return requiredTransformIds.Count > 0
            && missingTransformIds.Length == 0;
    }

    internal static string CreateMissingTransformsMessage(
        IReadOnlyList<string> requiredTransformIds,
        IReadOnlyList<string> missingTransformIds)
    {
        if (requiredTransformIds.Count == 0)
        {
            return "Save slot requires migration, but no concrete migration transform path could be inferred from its manifest.";
        }

        var missing = missingTransformIds.Count == 0
            ? "none"
            : string.Join(", ", missingTransformIds);
        return $"Save slot requires migration, but this runtime has no registered migration transforms for the required format path. Missing transforms: {missing}.";
    }

    internal static RuntimeSaveSlotMigrationResultData ApplyTransforms(
        string sourceDirectory,
        string targetDirectory,
        RuntimeSaveSlotInspectionData inspection)
    {
        if (inspection.MigrationPlan.RequiredTransforms.SequenceEqual(
                new[] { LegacySnapshotOnlySlotTransformId },
                StringComparer.Ordinal))
        {
            return ApplyLegacySnapshotOnlySlotTransform(
                sourceDirectory,
                targetDirectory,
                inspection);
        }

        if (CanApplyRuntimeSnapshotTransforms(inspection.MigrationPlan.RequiredTransforms))
        {
            return ApplyRuntimeSnapshotTransforms(
                sourceDirectory,
                targetDirectory,
                inspection);
        }

        return new RuntimeSaveSlotMigrationResultData(
            Success: false,
            MigrationApplied: false,
            Inspection: inspection,
            SourceDirectory: sourceDirectory,
            TargetDirectory: targetDirectory,
            AppliedTransforms: Array.Empty<string>(),
            MigrationIssues: new[]
            {
                new RuntimeSaveSnapshotDocumentIssueData(
                    "slot.migration",
                    null,
                    "Save slot migration transform execution is not implemented for this runtime.")
            });
    }

    private static bool CanApplyRuntimeSnapshotTransforms(
        IReadOnlyList<string> requiredTransforms)
    {
        return requiredTransforms.Count > 0
            && requiredTransforms.Any(static transform => transform.StartsWith("runtime_snapshot:", StringComparison.Ordinal))
            && requiredTransforms.All(transform =>
                string.Equals(transform, RuntimeSnapshot4To5TransformId, StringComparison.Ordinal)
                || string.Equals(transform, RuntimeSnapshot5To6TransformId, StringComparison.Ordinal)
                || string.Equals(transform, LegacySnapshotOnlySlotTransformId, StringComparison.Ordinal));
    }

    private static RuntimeSaveSlotMigrationResultData ApplyLegacySnapshotOnlySlotTransform(
        string sourceDirectory,
        string targetDirectory,
        RuntimeSaveSlotInspectionData inspection)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || string.IsNullOrWhiteSpace(targetDirectory))
        {
            return Failed(
                sourceDirectory,
                targetDirectory,
                inspection,
                "Save slot migration source and target directories must not be blank.");
        }

        var sourcePath = Path.GetFullPath(sourceDirectory);
        var targetPath = Path.GetFullPath(targetDirectory);
        if (string.Equals(sourcePath, targetPath, StringComparison.Ordinal))
        {
            return Failed(
                sourceDirectory,
                targetDirectory,
                inspection,
                "Save slot migration target directory must differ from the source directory.");
        }

        try
        {
            var document = RuntimeSaveSnapshotDocumentStore.ReadUnchecked(sourceDirectory);
            var validation = RuntimeSaveSnapshotDocumentVerifier.Validate(document);
            if (!validation.Success)
            {
                return new RuntimeSaveSlotMigrationResultData(
                    Success: false,
                    MigrationApplied: false,
                    Inspection: inspection,
                    SourceDirectory: sourceDirectory,
                    TargetDirectory: targetDirectory,
                    AppliedTransforms: Array.Empty<string>(),
                    MigrationIssues: validation.Issues);
            }

            if (document.Manifest.FormatVersion != RuntimeSaveFormat.CurrentVersion)
            {
                return Failed(
                    sourceDirectory,
                    targetDirectory,
                    inspection,
                    "Legacy save slot transform can only create a slot manifest for the current Runtime snapshot document format.");
            }

            RuntimeSaveSnapshotDocumentStore.WriteAtomic(targetDirectory, document);
            return new RuntimeSaveSlotMigrationResultData(
                Success: true,
                MigrationApplied: true,
                Inspection: inspection,
                SourceDirectory: sourceDirectory,
                TargetDirectory: targetDirectory,
                AppliedTransforms: new[] { LegacySnapshotOnlySlotTransformId },
                MigrationIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }
        catch (Exception ex) when (IsMigrationIoException(ex))
        {
            return Failed(
                sourceDirectory,
                targetDirectory,
                inspection,
                ex.Message);
        }
    }

    private static RuntimeSaveSlotMigrationResultData ApplyRuntimeSnapshotTransforms(
        string sourceDirectory,
        string targetDirectory,
        RuntimeSaveSlotInspectionData inspection)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || string.IsNullOrWhiteSpace(targetDirectory))
        {
            return Failed(
                sourceDirectory,
                targetDirectory,
                inspection,
                "Save slot migration source and target directories must not be blank.");
        }

        var sourcePath = Path.GetFullPath(sourceDirectory);
        var targetPath = Path.GetFullPath(targetDirectory);
        if (string.Equals(sourcePath, targetPath, StringComparison.Ordinal))
        {
            return Failed(
                sourceDirectory,
                targetDirectory,
                inspection,
                "Save slot migration target directory must differ from the source directory.");
        }

        try
        {
            var document = RuntimeSaveSnapshotDocumentStore.ReadUnchecked(sourceDirectory);
            if (document.Manifest.FormatVersion >= RuntimeSaveFormat.CurrentVersion)
            {
                return Failed(
                    sourceDirectory,
                    targetDirectory,
                    inspection,
                    "Runtime snapshot migration can only transform older Runtime save format documents.");
            }

            var migratedDocument = document;
            var appliedTransforms = new List<string>();
            while (migratedDocument.Manifest.FormatVersion < RuntimeSaveFormat.CurrentVersion)
            {
                var transformId = CreateRuntimeSnapshotTransformId(
                    migratedDocument.Manifest.FormatVersion,
                    migratedDocument.Manifest.FormatVersion + 1);
                if (!inspection.MigrationPlan.RequiredTransforms.Contains(transformId, StringComparer.Ordinal))
                {
                    return Failed(
                        sourceDirectory,
                        targetDirectory,
                        inspection,
                        $"Runtime snapshot migration is missing required transform '{transformId}'.");
                }

                migratedDocument = ApplyRuntimeSnapshotTransform(migratedDocument, transformId);
                appliedTransforms.Add(transformId);
            }

            var validation = RuntimeSaveSnapshotDocumentVerifier.Validate(migratedDocument);
            if (!validation.Success)
            {
                return new RuntimeSaveSlotMigrationResultData(
                    Success: false,
                    MigrationApplied: false,
                    Inspection: inspection,
                    SourceDirectory: sourceDirectory,
                    TargetDirectory: targetDirectory,
                    AppliedTransforms: Array.Empty<string>(),
                    MigrationIssues: validation.Issues);
            }

            RuntimeSaveSnapshotDocumentStore.WriteAtomic(targetDirectory, migratedDocument);
            if (inspection.MigrationPlan.RequiredTransforms.Contains(LegacySnapshotOnlySlotTransformId, StringComparer.Ordinal))
            {
                appliedTransforms.Add(LegacySnapshotOnlySlotTransformId);
            }

            return new RuntimeSaveSlotMigrationResultData(
                Success: true,
                MigrationApplied: true,
                Inspection: inspection,
                SourceDirectory: sourceDirectory,
                TargetDirectory: targetDirectory,
                AppliedTransforms: appliedTransforms.ToArray(),
                MigrationIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }
        catch (Exception ex) when (IsMigrationIoException(ex))
        {
            return Failed(
                sourceDirectory,
                targetDirectory,
                inspection,
                ex.Message);
        }
    }

    private static RuntimeSaveSnapshotDocumentData ApplyRuntimeSnapshotTransform(
        RuntimeSaveSnapshotDocumentData document,
        string transformId)
    {
        if (string.Equals(transformId, RuntimeSnapshot4To5TransformId, StringComparison.Ordinal)
            || string.Equals(transformId, RuntimeSnapshot5To6TransformId, StringComparison.Ordinal))
        {
            return document with
            {
                Manifest = document.Manifest with
                {
                    FormatVersion = document.Manifest.FormatVersion + 1
                }
            };
        }

        throw new InvalidOperationException($"Runtime snapshot migration transform '{transformId}' is not registered.");
    }

    private static RuntimeSaveSlotMigrationResultData Failed(
        string sourceDirectory,
        string targetDirectory,
        RuntimeSaveSlotInspectionData inspection,
        string message)
    {
        return new RuntimeSaveSlotMigrationResultData(
            Success: false,
            MigrationApplied: false,
            Inspection: inspection,
            SourceDirectory: sourceDirectory,
            TargetDirectory: targetDirectory,
            AppliedTransforms: Array.Empty<string>(),
            MigrationIssues: new[]
            {
                new RuntimeSaveSnapshotDocumentIssueData(
                    "slot.migration",
                    null,
                    message)
            });
    }

    private static bool IsMigrationIoException(Exception ex)
    {
        return ex is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException
            or System.Security.SecurityException;
    }

    private static string CreateSlotTransformId(int sourceVersion, int targetVersion)
    {
        return $"slot:{sourceVersion}->{targetVersion}";
    }

    private static string CreateRuntimeSnapshotTransformId(int sourceVersion, int targetVersion)
    {
        return $"runtime_snapshot:{sourceVersion}->{targetVersion}";
    }
}
