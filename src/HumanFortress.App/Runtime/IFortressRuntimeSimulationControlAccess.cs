using HumanFortress.Contracts.Runtime;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeSimulationControlAccess
{
    SimulationStatus ToggleSimulationPause();
    SimulationStatus CycleSimulationSpeedDown();
    SimulationStatus CycleSimulationSpeedUp();
}
