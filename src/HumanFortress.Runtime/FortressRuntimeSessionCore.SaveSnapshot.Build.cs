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
}
