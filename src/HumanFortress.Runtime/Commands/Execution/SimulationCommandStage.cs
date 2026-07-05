using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Executes queued simulation commands at the authoritative pre-read tick boundary.
/// </summary>
internal sealed class SimulationCommandStage
{
    private readonly CommandQueue _commandQueue;
    private readonly IRuntimeCommandClockContext _clockContext;
    private readonly ISimulationContext _commandContext;

    internal SimulationCommandStage(
        CommandQueue commandQueue,
        IRuntimeCommandClockContext clockContext,
        ISimulationContext commandContext)
    {
        _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
        _clockContext = clockContext ?? throw new ArgumentNullException(nameof(clockContext));
        _commandContext = commandContext ?? throw new ArgumentNullException(nameof(commandContext));
    }

    internal void Execute(ulong tick)
    {
        _clockContext.SetCurrentTick(tick);
        _commandQueue.ExecuteCommands(tick, _commandContext);
    }
}
