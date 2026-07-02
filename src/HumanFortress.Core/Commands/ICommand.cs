using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;

namespace HumanFortress.Core.Commands;

/// <summary>
/// Base interface for all game commands per UI_AND_INPUT_MODEL.md.
/// Commands are the only way for UI to affect simulation state.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Tick when this command should be executed.
    /// </summary>
    ulong Tick { get; }

    /// <summary>
    /// Stable identifier for debugging, replay diagnostics, and external correlation.
    /// Duplicate payload commands may share a value until a runtime/session wrapper adds
    /// enqueue identity; execution order is owned by CommandQueue, not by this value.
    /// </summary>
    Guid CommandId { get; }

    /// <summary>
    /// Command type for serialization.
    /// </summary>
    string CommandType { get; }

    /// <summary>
    /// Execute the command within the simulation context.
    /// </summary>
    void Execute(ISimulationContext context);

    /// <summary>
    /// Serialize command data for save/replay.
    /// </summary>
    byte[] Serialize();
}

/// <summary>
/// Context provided to commands during execution.
/// </summary>
public interface ISimulationContext
{
    DiffLog DiffLog { get; }
    ulong CurrentTick { get; }
    IWorldReader World { get; }
    IEventBus EventBus { get; }
}

/// <summary>
/// Read-only world access for commands.
/// </summary>
public interface IWorldReader
{
    // Will be expanded with actual world query methods
    bool IsValidPosition(int x, int y, int z);
}
