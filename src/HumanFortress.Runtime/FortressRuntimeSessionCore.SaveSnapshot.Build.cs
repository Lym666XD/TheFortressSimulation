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
        var runtimeSession = _runtimeSession;
        WorldSavePayloadData? worldPayload = null;
        RuntimeSaveMiningJobsData? miningJobs = null;
        RuntimeSaveTransportJobsData? transportJobs = null;
        RuntimeSaveCraftJobsData? craftJobs = null;
        if (runtimeSession != null)
        {
            worldPayload = WorldSavePayloadBuilder.Build(runtimeSession.World);
            var systems = runtimeSession.Host.Systems;
            if (systems == null)
            {
                miningJobs = RuntimeSaveSnapshotEmptyJobState.CreateMiningDocumentData();
                transportJobs = RuntimeSaveSnapshotEmptyJobState.CreateTransportDocumentData();
                craftJobs = RuntimeSaveSnapshotEmptyJobState.CreateCraftDocumentData();
            }
            else
            {
                miningJobs = RuntimeSaveSnapshotDocumentMiningMapper.ToDocumentData(
                    systems.MiningJobs.GetReplaySnapshot());
                transportJobs = RuntimeSaveSnapshotDocumentTransportMapper.ToDocumentData(
                    systems.TransportQueue.GetStateSnapshot(),
                    systems.TransportJobs.GetReplaySnapshot());
                craftJobs = RuntimeSaveSnapshotDocumentCraftMapper.ToDocumentData(
                    systems.CraftJobs.GetReplaySnapshot());
            }
        }

        var snapshot = new RuntimeSaveSnapshotData(
            BuildSaveManifestData(commandQueueSnapshot, rngStreamSnapshot),
            worldPayload,
            miningJobs,
            transportJobs,
            craftJobs,
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
        var runtimeSession = _runtimeSession;
        var worldSnapshot = runtimeSession == null
            ? (WorldSaveSnapshot?)null
            : WorldSaveSnapshotBuilder.Build(runtimeSession.World);
        return RuntimeSaveManifestBuilder.Build(checkpoint, _runtimeContentSnapshot, worldSnapshot);
    }
}
