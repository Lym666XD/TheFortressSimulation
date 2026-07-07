using HumanFortress.Contracts.Runtime;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationStatus SimulationStatus => _read.SimulationStatus;

    SimulationStatus IFortressRuntimeReadAccess.SimulationStatus => SimulationStatus;
}
