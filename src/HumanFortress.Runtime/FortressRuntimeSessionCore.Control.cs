using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Runtime;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    private SimulationStatus CurrentSimulationStatus => new(
        _tickScheduler.CurrentTick,
        _tickScheduler.IsPaused,
        _tickScheduler.SpeedMultiplier);

    SimulationStatus IFortressRuntimeSessionReadPort.SimulationStatus => CurrentSimulationStatus;

    private void EnqueueCurrentTickCommand(Func<ulong, ICommand> commandFactory)
    {
        ArgumentNullException.ThrowIfNull(commandFactory);
        _commandQueue.Enqueue(commandFactory(_tickScheduler.CurrentTick));
    }

    SimulationStatus IFortressRuntimeSessionSimulationControlPort.ToggleSimulationPause()
    {
        _tickScheduler.TogglePause();
        return CurrentSimulationStatus;
    }

    SimulationStatus IFortressRuntimeSessionSimulationControlPort.CycleSimulationSpeedDown()
    {
        _tickScheduler.CycleSpeedDown();
        return CurrentSimulationStatus;
    }

    SimulationStatus IFortressRuntimeSessionSimulationControlPort.CycleSimulationSpeedUp()
    {
        _tickScheduler.CycleSpeedUp();
        return CurrentSimulationStatus;
    }
}
