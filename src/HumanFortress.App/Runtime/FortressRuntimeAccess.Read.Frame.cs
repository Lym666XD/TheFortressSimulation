using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationAppFrameData GetCommittedAppFrame(SimulationAppFrameRequestData request)
    {
        var frame = _read.GetCommittedAppFrame(request);
        RememberCommittedFrame(request, frame);
        return frame;
    }

    SimulationAppFrameData IFortressRuntimeReadAccess.GetCommittedAppFrame(
        SimulationAppFrameRequestData request) =>
        GetCommittedAppFrame(request);
}
