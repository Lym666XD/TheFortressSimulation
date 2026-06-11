using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime;

/// <summary>
/// Executes queued simulation commands at the authoritative pre-read tick boundary.
/// </summary>
public sealed class SimulationCommandStage
{
    private readonly CommandQueue _commandQueue;
    private readonly IRuntimeCommandContext _context;

    public SimulationCommandStage(CommandQueue commandQueue, IRuntimeCommandContext context)
    {
        _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Execute(ulong tick)
    {
        _context.SetCurrentTick(tick);
        _commandQueue.ExecuteCommands(tick, _context);
    }
}
