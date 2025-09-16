using System.Collections.Concurrent;

namespace HumanFortress.Core.Commands;

/// <summary>
/// Deterministic command queue for input handling per UI_AND_INPUT_MODEL.md.
/// Commands are tagged with target tick and executed in order.
/// </summary>
public sealed class CommandQueue
{
    private readonly ConcurrentQueue<ICommand> _pendingCommands = new();
    private readonly List<ICommand> _executedCommands = new();
    private readonly object _executeLock = new();

    /// <summary>
    /// Enqueue a command for execution. Thread-safe.
    /// </summary>
    public void Enqueue(ICommand command)
    {
        _pendingCommands.Enqueue(command);
    }

    /// <summary>
    /// Execute all commands scheduled for the given tick.
    /// </summary>
    public void ExecuteCommands(ulong currentTick, ISimulationContext context)
    {
        lock (_executeLock)
        {
            var commandsToExecute = new List<ICommand>();

            // Collect commands for this tick
            while (_pendingCommands.TryPeek(out var command))
            {
                if (command.Tick <= currentTick)
                {
                    if (_pendingCommands.TryDequeue(out var dequeued))
                    {
                        commandsToExecute.Add(dequeued);
                    }
                }
                else
                {
                    break; // Commands are ordered by tick
                }
            }

            // Sort by command ID for determinism
            commandsToExecute.Sort((a, b) => a.CommandId.CompareTo(b.CommandId));

            // Execute in deterministic order
            foreach (var command in commandsToExecute)
            {
                try
                {
                    command.Execute(context);
                    _executedCommands.Add(command);
                }
                catch (Exception ex)
                {
                    HandleCommandError(command, ex);
                }
            }
        }
    }

    /// <summary>
    /// Get all executed commands for replay/save.
    /// </summary>
    public IReadOnlyList<ICommand> GetExecutedCommands()
    {
        lock (_executeLock)
        {
            return _executedCommands.ToList();
        }
    }

    /// <summary>
    /// Clear executed commands (after save).
    /// </summary>
    public void ClearExecutedCommands()
    {
        lock (_executeLock)
        {
            _executedCommands.Clear();
        }
    }

    /// <summary>
    /// Restore commands from save/replay.
    /// </summary>
    public void RestoreCommands(IEnumerable<ICommand> commands)
    {
        lock (_executeLock)
        {
            _executedCommands.Clear();
            _executedCommands.AddRange(commands);

            // Re-queue any future commands
            foreach (var cmd in commands)
            {
                _pendingCommands.Enqueue(cmd);
            }
        }
    }

    private void HandleCommandError(ICommand command, Exception ex)
    {
        // Per ERROR_HANDLING_POLICY.md: log and continue
        Console.WriteLine($"[ERROR] Command {command.CommandType} ({command.CommandId}) failed: {ex.Message}");
    }
}