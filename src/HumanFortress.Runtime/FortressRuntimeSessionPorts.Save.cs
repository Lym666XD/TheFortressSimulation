using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime;

internal interface IFortressRuntimeSessionSaveManifestPort
{
    RuntimeSaveManifestData GetSaveManifestData();
}

internal interface IFortressRuntimeSessionSaveSnapshotPort
{
    RuntimeSaveSnapshotDocumentData CreateSaveSnapshotDocumentData();
    void WriteSaveSnapshotDocument(string directory);
    RuntimeSaveSnapshotDocumentData ReadSaveSnapshotDocument(string directory);
    RuntimeSaveSlotInspectionData InspectSaveSnapshotDirectory(string directory);
    RuntimeSaveSlotMigrationResultData MigrateSaveSnapshotDirectory(
        string sourceDirectory,
        string targetDirectory);
    RuntimeSaveSnapshotDocumentValidationResultData ValidateSaveSnapshotDirectory(string directory);
    RuntimeSaveSnapshotDocumentValidationResultData ValidateSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document);
    RuntimeSaveSnapshotRestoreResultData RestorePendingCommandsFromSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document);
    RuntimeSaveSnapshotRestoreResultData RestorePendingCommandsFromSaveSnapshotDirectory(
        string directory);
    RuntimeSaveWorldSnapshotRestoreResultData RestoreWorldFromSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document);
    RuntimeSaveWorldSnapshotRestoreResultData RestoreWorldFromSaveSnapshotDirectory(
        string directory);
    RuntimeSaveFullSnapshotRestoreResultData RestoreFullFromSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document);
    RuntimeSaveFullSnapshotRestoreResultData RestoreFullFromSaveSnapshotDirectory(
        string directory);
}
