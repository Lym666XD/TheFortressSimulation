using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal void WriteSaveSnapshotDocument(string directory)
    {
        _saveSnapshots.WriteSaveSnapshotDocument(directory);
    }

    internal RuntimeSaveSnapshotDocumentValidationResultData ValidateSaveSnapshotDirectory(string directory)
    {
        return _saveSnapshots.ValidateSaveSnapshotDirectory(directory);
    }

    internal RuntimeSaveSnapshotRestoreResultData RestorePendingCommandsFromSaveSnapshotDirectory(string directory)
    {
        return _saveSnapshots.RestorePendingCommandsFromSaveSnapshotDirectory(directory);
    }

    internal RuntimeSaveWorldSnapshotRestoreResultData RestoreWorldFromSaveSnapshotDirectory(string directory)
    {
        return _saveSnapshots.RestoreWorldFromSaveSnapshotDirectory(directory);
    }

    internal RuntimeSaveFullSnapshotRestoreResultData RestoreFullFromSaveSnapshotDirectory(string directory)
    {
        return _saveSnapshots.RestoreFullFromSaveSnapshotDirectory(directory);
    }

    void IFortressRuntimeSaveAccess.WriteSaveSnapshotDocument(string directory) =>
        WriteSaveSnapshotDocument(directory);

    RuntimeSaveSnapshotDocumentValidationResultData IFortressRuntimeSaveAccess.ValidateSaveSnapshotDirectory(
        string directory) =>
        ValidateSaveSnapshotDirectory(directory);

    RuntimeSaveSnapshotRestoreResultData IFortressRuntimeSaveAccess.RestorePendingCommandsFromSaveSnapshotDirectory(
        string directory) =>
        RestorePendingCommandsFromSaveSnapshotDirectory(directory);

    RuntimeSaveWorldSnapshotRestoreResultData IFortressRuntimeSaveAccess.RestoreWorldFromSaveSnapshotDirectory(
        string directory) =>
        RestoreWorldFromSaveSnapshotDirectory(directory);

    RuntimeSaveFullSnapshotRestoreResultData IFortressRuntimeSaveAccess.RestoreFullFromSaveSnapshotDirectory(
        string directory) =>
        RestoreFullFromSaveSnapshotDirectory(directory);
}
