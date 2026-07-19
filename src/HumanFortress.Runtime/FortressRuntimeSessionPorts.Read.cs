using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime;

public interface IFortressRuntimeSessionReadPort
{
    SimulationAppFrameData GetCommittedAppFrame(SimulationAppFrameRequestData request);
}
