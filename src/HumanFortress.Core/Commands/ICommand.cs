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
    /// Unique identifier for replay and debugging.
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