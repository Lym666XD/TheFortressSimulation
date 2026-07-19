using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Runtime.Commands;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    private SimulationStatus CurrentSimulationStatus => new(
        _services.TickScheduler.CurrentTick,
        _services.TickScheduler.IsPaused,
        _services.TickScheduler.SpeedMultiplier);

    SimulationStatus IFortressRuntimeSessionSnapshotPort.SimulationStatus => CurrentSimulationStatus;

    private void EnqueueCurrentTickCommand(Func<ulong, ICommand> commandFactory)
    {
        ArgumentNullException.ThrowIfNull(commandFactory);
        var command = commandFactory(_services.TickScheduler.CurrentTick);
        var identifiedCommand = new RuntimeIdentifiedCommand(
            command,
            _services.NextCommandIdentitySequence());
        _services.CommandQueue.Enqueue(identifiedCommand);
    }

    SimulationStatus IFortressRuntimeSessionSimulationControlPort.ToggleSimulationPause()
    {
        _services.TickScheduler.TogglePause();
        return CurrentSimulationStatus;
    }

    SimulationStatus IFortressRuntimeSessionSimulationControlPort.CycleSimulationSpeedDown()
    {
        _services.TickScheduler.CycleSpeedDown();
        return CurrentSimulationStatus;
    }

    SimulationStatus IFortressRuntimeSessionSimulationControlPort.CycleSimulationSpeedUp()
    {
        _services.TickScheduler.CycleSpeedUp();
        return CurrentSimulationStatus;
    }
}
