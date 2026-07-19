using System.Text.Json;
using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Runtime.Save;
using HumanFortress.Runtime.Session;
using HumanFortress.Simulation.Save;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    private RuntimeSaveSnapshotRestoreResultData RestorePendingCommandsFromSaveSnapshotDocumentCore(
        RuntimeSaveSnapshotDocumentData document,
        RuntimeSaveSnapshotDocumentValidationResultData? prevalidated = null)
    {
        var validation = prevalidated ?? RuntimeSaveSnapshotDocumentVerifier.Validate(document);
        if (!validation.Success)
        {
            return new RuntimeSaveSnapshotRestoreResultData(
                Success: false,
                Validation: validation,
                PendingRecordCount: 0,
                RestoredCommandCount: 0,
                MaxCommandIdentitySequence: 0,
                RestoreIssues: Array.Empty<RuntimeSaveSnapshotDocumentIssueData>());
        }

        var contentCompatibility = EvaluateSaveContentCompatibility(document);
        if (!contentCompatibility.CanBindContent)
        {
            return new RuntimeSaveSnapshotRestoreResultData(
                Success: false,
                Validation: validation,
                PendingRecordCount: document.PendingCommandRecords?.Length ?? 0,
                RestoredCommandCount: 0,
                MaxCommandIdentitySequence: 0,
                RestoreIssues: new[]
                {
                    RuntimeSaveSlotContentCompatibilityPolicy.CreateBlockingIssue(
                        contentCompatibility)
                });
        }

        return RuntimeSaveSnapshotReplayRestorer.RestorePendingCommands(
            _services,
            document,
            validation);
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

        if (_runtimeContentSnapshot != null)
        {
            var activeContentCompatibility = EvaluateSaveContentCompatibility(document);
            if (!activeContentCompatibility.CanBindContent)
            {
                return CreateWorldContentCompatibilityFailure(
                    validation,
                    document.WorldPayload.Value.ReplayHash,
                    string.Empty,
                    0,
                    0,
                    activeContentCompatibility);
            }
        }

        var restore = WorldSavePayloadRestorer.RestoreSupportedSections(document.WorldPayload.Value);
        var restoreIssues = restore.Issues
            .Select(issue => new RuntimeSaveSnapshotDocumentIssueData("world.payload", null, issue))
            .ToArray();

        if (restore.Success && restore.World != null)
        {
            var stagedSession = CreateStagedRuntimeSession(
                restore.World,
                rebuildNavigation: true,
                out var stagedServices,
                out var stagedContentSnapshot);

            var restoredContentCompatibility = EvaluateSaveContentCompatibility(document, stagedContentSnapshot);
            if (!restoredContentCompatibility.CanBindContent)
            {
                return CreateWorldContentCompatibilityFailure(
                    validation,
                    restore.SavedWorldHash,
                    restore.RestoredWorldHash,
                    restore.RestoredChunkCount,
                    restore.RestoredTileCount,
                    restoredContentCompatibility);
            }

            CommitStagedRuntimeSession(
                stagedServices,
                stagedSession,
                stagedContentSnapshot);
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

    private RuntimeSaveSlotContentCompatibilityData EvaluateSaveContentCompatibility(
        RuntimeSaveSnapshotDocumentData document)
    {
        return EvaluateSaveContentCompatibility(document, _runtimeContentSnapshot);
    }

    private static RuntimeSaveSlotContentCompatibilityData EvaluateSaveContentCompatibility(
        RuntimeSaveSnapshotDocumentData document,
        FortressRuntimeContentSnapshot? runtimeContentSnapshot)
    {
        return RuntimeSaveSlotContentCompatibilityPolicy.Evaluate(
            document.Manifest.Content,
            document.Manifest.ContentCatalog,
            runtimeContentSnapshot);
    }

    private static RuntimeSaveWorldSnapshotRestoreResultData CreateWorldContentCompatibilityFailure(
        RuntimeSaveSnapshotDocumentValidationResultData validation,
        string savedWorldHash,
        string restoredWorldHash,
        int restoredChunkCount,
        int restoredTileCount,
        RuntimeSaveSlotContentCompatibilityData contentCompatibility)
    {
        return new RuntimeSaveWorldSnapshotRestoreResultData(
            Success: false,
            Validation: validation,
            SavedWorldHash: savedWorldHash,
            RestoredWorldHash: restoredWorldHash,
            RestoredChunkCount: restoredChunkCount,
            RestoredTileCount: restoredTileCount,
            RestoreIssues: new[]
            {
                RuntimeSaveSlotContentCompatibilityPolicy.CreateBlockingIssue(
                    contentCompatibility)
            });
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

        var unsupportedJobStateSections = RuntimeSaveJobStateRestorePolicy.GetPresentUnsupportedSections(document);
        if (unsupportedJobStateSections.Length > 0)
        {
            return new RuntimeSaveFullSnapshotRestoreResultData(
                Success: false,
                Validation: validation,
                SavedWorldHash: document.WorldPayload?.ReplayHash ?? string.Empty,
                RestoredWorldHash: string.Empty,
                RestoredChunkCount: 0,
                RestoredTileCount: 0,
                RestoredRngStreamCount: 0,
                PendingRecordCount: document.PendingCommandRecords?.Length ?? 0,
                RestoredCommandCount: 0,
                MaxCommandIdentitySequence: 0,
                RestoreIssues: new[]
                {
                    RuntimeSaveJobStateRestorePolicy.CreateBlockingIssue(
                        unsupportedJobStateSections)
                });
        }

        if (!document.WorldPayload.HasValue)
        {
            return new RuntimeSaveFullSnapshotRestoreResultData(
                Success: false,
                Validation: validation,
                SavedWorldHash: string.Empty,
                RestoredWorldHash: string.Empty,
                RestoredChunkCount: 0,
                RestoredTileCount: 0,
                RestoredRngStreamCount: 0,
                PendingRecordCount: document.PendingCommandRecords?.Length ?? 0,
                RestoredCommandCount: 0,
                MaxCommandIdentitySequence: 0,
                RestoreIssues: new[]
                {
                    new RuntimeSaveSnapshotDocumentIssueData(
                        "world.payload",
                        null,
                        "Save snapshot document does not contain a world payload.")
                });
        }

        if (_runtimeContentSnapshot != null)
        {
            var activeContentCompatibility = EvaluateSaveContentCompatibility(document);
            if (!activeContentCompatibility.CanBindContent)
            {
                return new RuntimeSaveFullSnapshotRestoreResultData(
                    Success: false,
                    Validation: validation,
                    SavedWorldHash: document.WorldPayload.Value.ReplayHash,
                    RestoredWorldHash: string.Empty,
                    RestoredChunkCount: 0,
                    RestoredTileCount: 0,
                    RestoredRngStreamCount: 0,
                    PendingRecordCount: document.PendingCommandRecords?.Length ?? 0,
                    RestoredCommandCount: 0,
                    MaxCommandIdentitySequence: 0,
                    RestoreIssues: new[]
                    {
                        RuntimeSaveSlotContentCompatibilityPolicy.CreateBlockingIssue(
                            activeContentCompatibility)
                    });
            }
        }

        var worldRestore = WorldSavePayloadRestorer.RestoreSupportedSections(document.WorldPayload.Value);
        var worldRestoreIssues = worldRestore.Issues
            .Select(issue => new RuntimeSaveSnapshotDocumentIssueData("world.payload", null, issue))
            .ToArray();
        if (!worldRestore.Success || worldRestore.World == null)
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
                RestoreIssues: worldRestoreIssues);
        }

        var stagedSession = CreateStagedRuntimeSession(
            worldRestore.World,
            rebuildNavigation: true,
            out var stagedServices,
            out var stagedContentSnapshot);

        var stagedContentCompatibility = EvaluateSaveContentCompatibility(document, stagedContentSnapshot);
        if (!stagedContentCompatibility.CanBindContent)
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
                RestoreIssues: new[]
                {
                    RuntimeSaveSlotContentCompatibilityPolicy.CreateBlockingIssue(
                        stagedContentCompatibility)
                });
        }

        stagedSession.Host.AttachForManualTicks();
        var stagedRuntimeSession = new FortressRuntimeSession(stagedSession);
        var miningRestore = RuntimeSaveSnapshotMiningJobRestorer.Restore(stagedRuntimeSession, document);
        if (!miningRestore.Success)
        {
            var restoreIssues = worldRestoreIssues
                .Concat(miningRestore.RestoreIssues)
                .ToArray();
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
                RestoreIssues: restoreIssues);
        }

        var transportRestore = RuntimeSaveSnapshotTransportJobRestorer.Restore(stagedRuntimeSession, document);
        if (!transportRestore.Success)
        {
            var restoreIssues = worldRestoreIssues
                .Concat(miningRestore.RestoreIssues)
                .Concat(transportRestore.RestoreIssues)
                .ToArray();
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
                RestoreIssues: restoreIssues);
        }

        var craftRestore = RuntimeSaveSnapshotCraftJobRestorer.Restore(stagedRuntimeSession, document);
        if (!craftRestore.Success)
        {
            var restoreIssues = worldRestoreIssues
                .Concat(miningRestore.RestoreIssues)
                .Concat(transportRestore.RestoreIssues)
                .Concat(craftRestore.RestoreIssues)
                .ToArray();
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
                RestoreIssues: restoreIssues);
        }

        var rngRestore = RuntimeSaveSnapshotRngRestorer.Restore(stagedServices, document);
        if (!rngRestore.Success)
        {
            var restoreIssues = worldRestoreIssues
                .Concat(miningRestore.RestoreIssues)
                .Concat(transportRestore.RestoreIssues)
                .Concat(craftRestore.RestoreIssues)
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
            stagedServices,
            document,
            validation);
        var combinedIssues = worldRestoreIssues
            .Concat(miningRestore.RestoreIssues)
            .Concat(transportRestore.RestoreIssues)
            .Concat(craftRestore.RestoreIssues)
            .Concat(rngRestore.RestoreIssues)
            .Concat(commandRestore.RestoreIssues)
            .ToArray();

        if (commandRestore.Success)
        {
            CommitStagedRuntimeSession(
                stagedServices,
                stagedSession,
                stagedContentSnapshot);
        }

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
            var validation = RuntimeSaveSnapshotDocumentStore.ValidateDirectory(directory);
            if (!validation.Success)
            {
                document = default;
                failure = validation;
                return false;
            }

            document = RuntimeSaveSnapshotDocumentStore.ReadUnchecked(directory);
            failure = validation;
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
