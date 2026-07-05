using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Runtime.Save;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    RuntimeSaveManifestData IFortressRuntimeSessionSaveManifestPort.GetSaveManifestData()
    {
        return BuildSaveManifestData(commandQueueSnapshot: null, rngStreamSnapshot: null);
    }

    RuntimeSaveSnapshotDocumentData IFortressRuntimeSessionSaveSnapshotPort.CreateSaveSnapshotDocumentData()
    {
        return CreateSaveSnapshotDocumentDataCore();
    }

    void IFortressRuntimeSessionSaveSnapshotPort.WriteSaveSnapshotDocument(string directory)
    {
        RuntimeSaveSnapshotDocumentStore.WriteAtomic(directory, CreateSaveSnapshotDocumentDataCore());
    }

    RuntimeSaveSnapshotDocumentData IFortressRuntimeSessionSaveSnapshotPort.ReadSaveSnapshotDocument(string directory)
    {
        return RuntimeSaveSnapshotDocumentStore.Read(directory);
    }

    RuntimeSaveSnapshotDocumentValidationResultData IFortressRuntimeSessionSaveSnapshotPort.ValidateSaveSnapshotDirectory(
        string directory)
    {
        return TryReadUncheckedSaveSnapshotDocument(directory, out var document, out var failure)
            ? RuntimeSaveSnapshotDocumentVerifier.Validate(document)
            : failure;
    }

    RuntimeSaveSnapshotDocumentValidationResultData IFortressRuntimeSessionSaveSnapshotPort.ValidateSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document)
    {
        return RuntimeSaveSnapshotDocumentVerifier.Validate(document);
    }

    RuntimeSaveSnapshotRestoreResultData IFortressRuntimeSessionSaveSnapshotPort.RestorePendingCommandsFromSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document)
    {
        return RuntimeSaveSnapshotReplayRestorer.RestorePendingCommands(_services, document);
    }

    RuntimeSaveSnapshotRestoreResultData IFortressRuntimeSessionSaveSnapshotPort.RestorePendingCommandsFromSaveSnapshotDirectory(
        string directory)
    {
        if (!TryReadUncheckedSaveSnapshotDocument(directory, out var document, out var failure))
        {
            return new RuntimeSaveSnapshotRestoreResultData(
                Success: false,
                Validation: failure,
                PendingRecordCount: 0,
                RestoredCommandCount: 0,
                MaxCommandIdentitySequence: 0,
                RestoreIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }

        return RuntimeSaveSnapshotReplayRestorer.RestorePendingCommands(
            _services,
            document);
    }

    RuntimeSaveWorldSnapshotRestoreResultData IFortressRuntimeSessionSaveSnapshotPort.RestoreWorldFromSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document)
    {
        return RestoreWorldFromSaveSnapshotDocumentCore(document);
    }

    RuntimeSaveWorldSnapshotRestoreResultData IFortressRuntimeSessionSaveSnapshotPort.RestoreWorldFromSaveSnapshotDirectory(
        string directory)
    {
        if (!TryReadUncheckedSaveSnapshotDocument(directory, out var document, out var failure))
        {
            return new RuntimeSaveWorldSnapshotRestoreResultData(
                Success: false,
                Validation: failure,
                SavedWorldHash: string.Empty,
                RestoredWorldHash: string.Empty,
                RestoredChunkCount: 0,
                RestoredTileCount: 0,
                RestoreIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }

        return RestoreWorldFromSaveSnapshotDocumentCore(document);
    }

    RuntimeSaveFullSnapshotRestoreResultData IFortressRuntimeSessionSaveSnapshotPort.RestoreFullFromSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document)
    {
        return RestoreFullFromSaveSnapshotDocumentCore(document);
    }

    RuntimeSaveFullSnapshotRestoreResultData IFortressRuntimeSessionSaveSnapshotPort.RestoreFullFromSaveSnapshotDirectory(
        string directory)
    {
        if (!TryReadUncheckedSaveSnapshotDocument(directory, out var document, out var failure))
        {
            return new RuntimeSaveFullSnapshotRestoreResultData(
                Success: false,
                Validation: failure,
                SavedWorldHash: string.Empty,
                RestoredWorldHash: string.Empty,
                RestoredChunkCount: 0,
                RestoredTileCount: 0,
                RestoredRngStreamCount: 0,
                PendingRecordCount: 0,
                RestoredCommandCount: 0,
                MaxCommandIdentitySequence: 0,
                RestoreIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }

        return RestoreFullFromSaveSnapshotDocumentCore(document);
    }
}
