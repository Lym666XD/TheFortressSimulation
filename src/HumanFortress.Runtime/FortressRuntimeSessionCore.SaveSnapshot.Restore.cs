using System.Text.Json;
using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Runtime.Save;
using HumanFortress.Runtime.Session;
using HumanFortress.Simulation.Save;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
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
            _runtimeSession = new FortressRuntimeSession(_runtimeSessionFactory.CreateFromWorld(restore.World, rebuildNavigation: true));
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

    private static bool IsSaveSnapshotDirectoryReadException(Exception ex)
    {
        return ex is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException;
    }

    private static bool TryReadUncheckedSaveSnapshotDocument(
        string directory,
        out RuntimeSaveSnapshotDocumentData document,
        out RuntimeSaveSnapshotDocumentValidationResultData failure)
    {
        try
        {
            document = RuntimeSaveSnapshotDocumentStore.ReadUnchecked(directory);
            failure = RuntimeSaveSnapshotDocumentValidationResultData.Valid;
            return true;
        }
        catch (Exception ex) when (IsSaveSnapshotDirectoryReadException(ex))
        {
            document = default;
            failure = BuildSaveSnapshotDirectoryFailure(ex);
            return false;
        }
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
