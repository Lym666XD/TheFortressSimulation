using System.Text.Json;
using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Random;
using HumanFortress.Runtime.Replay;
using HumanFortress.Runtime.Save;
using HumanFortress.Simulation.Save;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    RuntimeReplayCheckpointData IFortressRuntimeSessionReplayCheckpointPort.GetReplayCheckpointData()
    {
        return RuntimeReplayCheckpointHashBuilder.BuildData(_services, _runtimeSession);
    }

    string IFortressRuntimeSessionReplayCheckpointPort.GetReplayCheckpointHash()
    {
        return RuntimeReplayCheckpointHashBuilder.BuildData(_services, _runtimeSession).AggregateHash;
    }

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
        try
        {
            var document = RuntimeSaveSnapshotDocumentStore.ReadUnchecked(directory);
            return RuntimeSaveSnapshotDocumentVerifier.Validate(document);
        }
        catch (Exception ex) when (IsSaveSnapshotDirectoryReadException(ex))
        {
            return BuildSaveSnapshotDirectoryFailure(ex);
        }
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
        RuntimeSaveSnapshotDocumentData document;
        try
        {
            document = RuntimeSaveSnapshotDocumentStore.ReadUnchecked(directory);
        }
        catch (Exception ex) when (IsSaveSnapshotDirectoryReadException(ex))
        {
            return new RuntimeSaveSnapshotRestoreResultData(
                Success: false,
                Validation: BuildSaveSnapshotDirectoryFailure(ex),
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
        RuntimeSaveSnapshotDocumentData document;
        try
        {
            document = RuntimeSaveSnapshotDocumentStore.ReadUnchecked(directory);
        }
        catch (Exception ex) when (IsSaveSnapshotDirectoryReadException(ex))
        {
            return new RuntimeSaveWorldSnapshotRestoreResultData(
                Success: false,
                Validation: BuildSaveSnapshotDirectoryFailure(ex),
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
        RuntimeSaveSnapshotDocumentData document;
        try
        {
            document = RuntimeSaveSnapshotDocumentStore.ReadUnchecked(directory);
        }
        catch (Exception ex) when (IsSaveSnapshotDirectoryReadException(ex))
        {
            return new RuntimeSaveFullSnapshotRestoreResultData(
                Success: false,
                Validation: BuildSaveSnapshotDirectoryFailure(ex),
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

    private RuntimeSaveSnapshotDocumentData CreateSaveSnapshotDocumentDataCore()
    {
        var commandQueueSnapshot = _services.CommandQueue.GetReplaySnapshot();
        var rngStreamSnapshot = _services.RngStreams.GetStateSnapshot();
        WorldSavePayloadData? worldPayload = _runtimeSession == null
            ? null
            : WorldSavePayloadBuilder.Build(_runtimeSession.World);
        var snapshot = new RuntimeSaveSnapshotData(
            BuildSaveManifestData(commandQueueSnapshot, rngStreamSnapshot),
            worldPayload,
            rngStreamSnapshot,
            commandQueueSnapshot.ExecutedRecords,
            commandQueueSnapshot.PendingRecords);
        return snapshot.ToDocumentData();
    }

    private RuntimeSaveWorldSnapshotRestoreResultData RestoreWorldFromSaveSnapshotDocumentCore(
        RuntimeSaveSnapshotDocumentData document,
        RuntimeSaveSnapshotDocumentValidationResultData? prevalidated = null)
    {
        var validation = prevalidated ?? RuntimeSaveSnapshotDocumentVerifier.Validate(document);
        if (!validation.Success)
        {
            return new RuntimeSaveWorldSnapshotRestoreResultData(
                Success: false,
                Validation: validation,
                SavedWorldHash: document.WorldPayload?.ReplayHash ?? string.Empty,
                RestoredWorldHash: string.Empty,
                RestoredChunkCount: 0,
                RestoredTileCount: 0,
                RestoreIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }

        if (!document.WorldPayload.HasValue)
        {
            return new RuntimeSaveWorldSnapshotRestoreResultData(
                Success: false,
                Validation: validation,
                SavedWorldHash: string.Empty,
                RestoredWorldHash: string.Empty,
                RestoredChunkCount: 0,
                RestoredTileCount: 0,
                RestoreIssues: new[]
                {
                    new RuntimeSaveSnapshotDocumentIssueData(
                        "world.payload",
                        null,
                        "Save snapshot document does not contain a world payload.")
                });
        }

        var restore = WorldSavePayloadRestorer.RestoreSupportedSections(document.WorldPayload.Value);
        var restoreIssues = restore.Issues
            .Select(issue => new RuntimeSaveSnapshotDocumentIssueData("world.payload", null, issue))
            .ToArray();

        if (restore.Success && restore.World != null)
        {
            StopIfRunningCore();
            _workshopCompletionNotifier.SetHandler(null);
            _runtimeSession = _runtimeSessionFactory.CreateFromWorld(restore.World, rebuildNavigation: true);
        }

        return new RuntimeSaveWorldSnapshotRestoreResultData(
            Success: restore.Success,
            Validation: validation,
            SavedWorldHash: restore.SavedWorldHash,
            RestoredWorldHash: restore.RestoredWorldHash,
            RestoredChunkCount: restore.RestoredChunkCount,
            RestoredTileCount: restore.RestoredTileCount,
            RestoreIssues: restoreIssues);
    }

    private RuntimeSaveFullSnapshotRestoreResultData RestoreFullFromSaveSnapshotDocumentCore(
        RuntimeSaveSnapshotDocumentData document)
    {
        var validation = RuntimeSaveSnapshotDocumentVerifier.Validate(document);
        if (!validation.Success)
        {
            return new RuntimeSaveFullSnapshotRestoreResultData(
                Success: false,
                Validation: validation,
                SavedWorldHash: document.WorldPayload?.ReplayHash ?? string.Empty,
                RestoredWorldHash: string.Empty,
                RestoredChunkCount: 0,
                RestoredTileCount: 0,
                RestoredRngStreamCount: 0,
                PendingRecordCount: 0,
                RestoredCommandCount: 0,
                MaxCommandIdentitySequence: 0,
                RestoreIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }

        var worldRestore = RestoreWorldFromSaveSnapshotDocumentCore(document, validation);
        if (!worldRestore.Success)
        {
            return new RuntimeSaveFullSnapshotRestoreResultData(
                Success: false,
                Validation: validation,
                SavedWorldHash: worldRestore.SavedWorldHash,
                RestoredWorldHash: worldRestore.RestoredWorldHash,
                RestoredChunkCount: worldRestore.RestoredChunkCount,
                RestoredTileCount: worldRestore.RestoredTileCount,
                RestoredRngStreamCount: 0,
                PendingRecordCount: 0,
                RestoredCommandCount: 0,
                MaxCommandIdentitySequence: 0,
                RestoreIssues: worldRestore.RestoreIssues);
        }

        var rngRestore = RuntimeSaveSnapshotRngRestorer.Restore(_services, document);
        if (!rngRestore.Success)
        {
            var restoreIssues = worldRestore.RestoreIssues
                .Concat(rngRestore.RestoreIssues)
                .ToArray();
            return new RuntimeSaveFullSnapshotRestoreResultData(
                Success: false,
                Validation: validation,
                SavedWorldHash: worldRestore.SavedWorldHash,
                RestoredWorldHash: worldRestore.RestoredWorldHash,
                RestoredChunkCount: worldRestore.RestoredChunkCount,
                RestoredTileCount: worldRestore.RestoredTileCount,
                RestoredRngStreamCount: rngRestore.RestoredStreamCount,
                PendingRecordCount: 0,
                RestoredCommandCount: 0,
                MaxCommandIdentitySequence: 0,
                RestoreIssues: restoreIssues);
        }

        var commandRestore = RuntimeSaveSnapshotReplayRestorer.RestorePendingCommands(
            _services,
            document,
            validation);
        var combinedIssues = worldRestore.RestoreIssues
            .Concat(rngRestore.RestoreIssues)
            .Concat(commandRestore.RestoreIssues)
            .ToArray();

        return new RuntimeSaveFullSnapshotRestoreResultData(
            Success: commandRestore.Success,
            Validation: validation,
            SavedWorldHash: worldRestore.SavedWorldHash,
            RestoredWorldHash: worldRestore.RestoredWorldHash,
            RestoredChunkCount: worldRestore.RestoredChunkCount,
            RestoredTileCount: worldRestore.RestoredTileCount,
            RestoredRngStreamCount: rngRestore.RestoredStreamCount,
            PendingRecordCount: commandRestore.PendingRecordCount,
            RestoredCommandCount: commandRestore.RestoredCommandCount,
            MaxCommandIdentitySequence: commandRestore.MaxCommandIdentitySequence,
            RestoreIssues: combinedIssues);
    }

    private RuntimeSaveManifestData BuildSaveManifestData(
        CommandQueueReplaySnapshot? commandQueueSnapshot,
        IReadOnlyList<RngStreamStateSnapshot>? rngStreamSnapshot)
    {
        var checkpoint = RuntimeReplayCheckpointHashBuilder.BuildData(
            _services,
            _runtimeSession,
            commandQueueSnapshot,
            rngStreamSnapshot);
        var worldSnapshot = _runtimeSession == null
            ? (WorldSaveSnapshot?)null
            : WorldSaveSnapshotBuilder.Build(_runtimeSession.World);
        return RuntimeSaveManifestBuilder.Build(checkpoint, _runtimeContentSnapshot, worldSnapshot);
    }

    private static bool IsSaveSnapshotDirectoryReadException(Exception ex)
    {
        return ex is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException;
    }

    private static RuntimeSaveSnapshotDocumentValidationResultData BuildSaveSnapshotDirectoryFailure(Exception ex)
    {
        return new RuntimeSaveSnapshotDocumentValidationResultData(
            Success: false,
            Issues: new[]
            {
                new RuntimeSaveSnapshotDocumentIssueData(
                    Section: "snapshot.document",
                    RecordIndex: null,
                    Message: ex.Message)
            });
    }
}
