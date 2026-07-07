using HumanFortress.Contracts.Runtime;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationStatus ToggleSimulationPause()
    {
        return _simulationControl.ToggleSimulationPause();
    }

    internal SimulationStatus CycleSimulationSpeedDown()
    {
        return _simulationControl.CycleSimulationSpeedDown();
    }

    internal SimulationStatus CycleSimulationSpeedUp()
    {
        return _simulationControl.CycleSimulationSpeedUp();
    }

    SimulationStatus IFortressRuntimeSimulationControlAccess.ToggleSimulationPause() => ToggleSimulationPause();

    SimulationStatus IFortressRuntimeSimulationControlAccess.CycleSimulationSpeedDown() => CycleSimulationSpeedDown();

    SimulationStatus IFortressRuntimeSimulationControlAccess.CycleSimulationSpeedUp() => CycleSimulationSpeedUp();
}
