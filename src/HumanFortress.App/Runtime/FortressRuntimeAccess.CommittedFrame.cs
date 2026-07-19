using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    private CommittedFrameCacheEntry? _latestCommittedFrame;

    private void RememberCommittedFrame(
        SimulationAppFrameRequestData request,
        SimulationAppFrameData frame)
    {
        if (!frame.IsAvailable)
        {
            Volatile.Write(ref _latestCommittedFrame, null);
            return;
        }

        Volatile.Write(
            ref _latestCommittedFrame,
            new CommittedFrameCacheEntry(request, frame));
    }

    private bool TryGetCommittedFrame(out CommittedFrameCacheEntry entry)
    {
        entry = Volatile.Read(ref _latestCommittedFrame)!;
        return entry != null && entry.Frame.IsAvailable;
    }

    private sealed record CommittedFrameCacheEntry(
        SimulationAppFrameRequestData Request,
        SimulationAppFrameData Frame);
}
