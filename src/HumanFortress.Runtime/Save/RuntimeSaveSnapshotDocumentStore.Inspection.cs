using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Content.Loading;

namespace HumanFortress.Runtime.Save;

internal static partial class RuntimeSaveSnapshotDocumentStore
{
    internal static RuntimeSaveSlotInspectionData InspectDirectory(
        string directory,
        FortressRuntimeContentSnapshot? currentContent = null)
    {
        RuntimeSaveSnapshotDocumentData document;
        var documentReadFailure = RuntimeSaveSnapshotDocumentValidationResultData.Valid;
        var documentReadSuccess = true;
        try
        {
            document = ReadUnchecked(directory);
        }
        catch (Exception ex) when (IsDirectoryReadException(ex))
        {
            document = default;
            documentReadSuccess = false;
            documentReadFailure = BuildDirectoryFailure("snapshot.document", ex);
        }

        RuntimeSaveSlotManifestData manifest;
        var slotManifestReadFailure = RuntimeSaveSnapshotDocumentValidationResultData.Valid;
        var slotManifestReadSuccess = true;
        var slotManifestMissing = false;
        try
        {
            manifest = ReadSlotManifestUnchecked(directory);
        }
        catch (Exception ex) when (IsDirectoryReadException(ex))
        {
            manifest = default;
            slotManifestReadSuccess = false;
            slotManifestMissing = ex is FileNotFoundException;
            slotManifestReadFailure = BuildDirectoryFailure("slot.manifest", ex);
        }

        if (!documentReadSuccess || !slotManifestReadSuccess)
        {
            if (documentReadSuccess && slotManifestMissing)
            {
                var legacyDocumentValidation = RuntimeSaveSnapshotDocumentVerifier.Validate(document);
                return BuildInspection(
                    CombineValidation(legacyDocumentValidation, slotManifestReadFailure),
                    RuntimeSaveSlotCompatibilityPolicy.EvaluateLegacySnapshotDocument(document),
                    documentAvailable: true,
                    document,
                    manifest: null,
                    currentContent);
            }

            return BuildInspection(
                CombineValidation(documentReadFailure, slotManifestReadFailure),
                slotManifestReadSuccess
                    ? RuntimeSaveSlotCompatibilityPolicy.Evaluate(manifest)
                    : RuntimeSaveSlotCompatibilityData.Unavailable,
                documentReadSuccess,
                document,
                slotManifestReadSuccess ? manifest : null,
                currentContent);
        }

        var documentValidation = RuntimeSaveSnapshotDocumentVerifier.Validate(document);
        var compatibility = RuntimeSaveSlotCompatibilityPolicy.Evaluate(manifest);
        var slotValidation = RuntimeSaveSlotManifestVerifier.Validate(manifest, document);

        if (documentValidation.Success && slotValidation.Success)
        {
            return BuildInspection(
                RuntimeSaveSnapshotDocumentValidationResultData.Valid,
                compatibility,
                documentAvailable: true,
                document,
                manifest,
                currentContent);
        }

        return BuildInspection(
            CombineValidation(documentValidation, slotValidation),
            compatibility,
            documentAvailable: true,
            document,
            manifest,
            currentContent);
    }

    private static RuntimeSaveSlotInspectionData BuildInspection(
        RuntimeSaveSnapshotDocumentValidationResultData validation,
        RuntimeSaveSlotCompatibilityData compatibility,
        bool documentAvailable,
        RuntimeSaveSnapshotDocumentData document,
        RuntimeSaveSlotManifestData? manifest,
        FortressRuntimeContentSnapshot? currentContent)
    {
        var contentCompatibility = documentAvailable
            ? RuntimeSaveSlotContentCompatibilityPolicy.Evaluate(
                document.Manifest.Content,
                document.Manifest.ContentCatalog,
                currentContent)
            : RuntimeSaveSlotContentCompatibilityData.Unavailable;

        return new RuntimeSaveSlotInspectionData(
            Success: validation.Success,
            Validation: validation,
            Compatibility: compatibility,
            ContentCompatibility: contentCompatibility,
            MigrationPlan: RuntimeSaveSlotMigrationPlanBuilder.Build(
                validation,
                compatibility,
                manifest),
            RestorePlan: RuntimeSaveSlotRestorePlanBuilder.Build(
                validation,
                compatibility,
                contentCompatibility,
                documentAvailable,
                document,
                manifest),
            Manifest: manifest);
    }

    private static RuntimeSaveSnapshotDocumentValidationResultData CombineValidation(
        RuntimeSaveSnapshotDocumentValidationResultData first,
        RuntimeSaveSnapshotDocumentValidationResultData second)
    {
        if (first.Success && second.Success)
            return RuntimeSaveSnapshotDocumentValidationResultData.Valid;

        return new RuntimeSaveSnapshotDocumentValidationResultData(
            false,
            first.Issues.Concat(second.Issues).ToArray());
    }
}
