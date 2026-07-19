using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Runtime.Replay;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    RuntimeReplayCheckpointData IFortressRuntimeSessionReplayCheckpointPort.GetReplayCheckpointData()
    {
        if (TryGetCommittedReplayCheckpoint(out var checkpoint))
            return checkpoint;

        EnsureLiveReplayReadIsSafe();
        return BuildLiveCommittedReplayCheckpoint();
    }

    string IFortressRuntimeSessionReplayCheckpointPort.GetReplayCheckpointHash()
    {
        if (TryGetCommittedReplayCheckpoint(out var checkpoint))
            return checkpoint.AggregateHash;

        EnsureLiveReplayReadIsSafe();
        return BuildLiveCommittedReplayCheckpoint().AggregateHash;
    }

    private RuntimeReplayCheckpointData BuildLiveCommittedReplayCheckpoint()
    {
        var replay = RuntimeReplayCheckpointHashBuilder.BuildData(_services, _runtimeSession);
        var systems = _runtimeSession?.Host.Systems;
        return systems == null
            ? replay
            : RuntimeCommittedReplayHashBuilder.Build(replay, systems).Replay;
    }

    private void EnsureLiveReplayReadIsSafe()
    {
        if (_services.TickScheduler.IsRunning)
        {
            throw new InvalidOperationException(
                "No committed replay checkpoint is available for the active runtime session.");
        }
    }
}
