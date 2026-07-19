using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeReadAccess
{
    SimulationAppFrameData GetCommittedAppFrame(SimulationAppFrameRequestData request);
}
