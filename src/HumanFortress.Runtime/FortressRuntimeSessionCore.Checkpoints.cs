using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Runtime.Checkpoints;
using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Runtime.Checkpoints;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    private readonly RuntimeCommittedCheckpointOwner _committedCheckpoints = new();
    private readonly RuntimeCommittedAppFramePublisher _committedAppFrames = new();

    bool IFortressRuntimeSessionCheckpointPort.TryGetLatestCheckpointIdentity(
        out RuntimeCheckpointIdentityData identity)
    {
        return _committedCheckpoints.TryGetLatestIdentity(out identity);
    }

    bool IFortressRuntimeSessionCheckpointPort.TryGetLatestCommittedDiagnostics(
        out RuntimeCommittedDiagnosticsData diagnostics)
    {
        return _committedCheckpoints.TryGetLatestDiagnostics(out diagnostics);
    }

    private void ActivateCheckpointGeneration(
        FortressRuntimeSession session,
        RuntimeSessionServices services,
        FortressRuntimeContentSnapshot? content)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(services);

        var generation = _committedCheckpoints.ActivateGeneration(content);
        _committedAppFrames.ActivateGeneration(generation, services, session);
        session.Host.SetPostTickCommitHandler((systems, tick) =>
            _committedCheckpoints.TryPublishCommitted(
                generation,
                services,
                session,
                systems,
                tick,
                _committedAppFrames,
                out _));
    }

    internal void InvalidateCheckpointGeneration()
    {
        var generation = _committedCheckpoints.InvalidateActiveGeneration();
        if (generation != null)
            _committedAppFrames.InvalidateGeneration(generation);
    }

    private SimulationAppFrameData GetCommittedAppFrameCore(
        SimulationAppFrameRequestData request)
    {
        return _committedAppFrames.GetCommittedFrame(request);
    }

    private bool TryGetCommittedReplayCheckpoint(
        out RuntimeReplayCheckpointData checkpoint)
    {
        return _committedCheckpoints.TryGetLatestReplay(out checkpoint);
    }
}
