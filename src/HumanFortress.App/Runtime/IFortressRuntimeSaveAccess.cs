using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeSaveAccess
{
    void WriteSaveSnapshotDocument(string directory);

    RuntimeSaveSnapshotDocumentValidationResultData ValidateSaveSnapshotDirectory(string directory);

    RuntimeSaveSnapshotRestoreResultData RestorePendingCommandsFromSaveSnapshotDirectory(string directory);

    RuntimeSaveWorldSnapshotRestoreResultData RestoreWorldFromSaveSnapshotDirectory(string directory);

    RuntimeSaveFullSnapshotRestoreResultData RestoreFullFromSaveSnapshotDirectory(string directory);
}
