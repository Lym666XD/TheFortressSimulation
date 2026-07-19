using HumanFortress.App.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Rendering;

internal sealed class FortressViewReadRuntimePorts
{
    private readonly IFortressRuntimeReadAccess _runtime;

    internal FortressViewReadRuntimePorts(IFortressRuntimeReadAccess runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    internal SimulationAppFrameData GetCommittedAppFrame(SimulationAppFrameRequestData request) =>
        _runtime.GetCommittedAppFrame(request);
}
