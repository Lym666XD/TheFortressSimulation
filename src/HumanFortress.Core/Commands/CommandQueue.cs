using System.Collections.Concurrent;
using HumanFortress.Contracts.Diagnostics;

namespace HumanFortress.Core.Commands;

/// <summary>
/// Deterministic command queue for input handling per UI_AND_INPUT_MODEL.md.
/// Commands are tagged with target tick and executed in order.
/// </summary>
public sealed class CommandQueue
{
    private readonly ConcurrentQueue<QueuedCommand> _pendingCommands = new();
    private readonly List<ICommand> _executedCommands = new();
    private readonly object _executeLock = new();
    private long _nextSequence;

    /// <summary>
    /// Enqueue a command for execution. Thread-safe.
    /// </summary>
    public void Enqueue(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        lock (_executeLock)
        {
            var sequence = ++_nextSequence;
            _pendingCommands.Enqueue(new QueuedCommand(command, sequence));
        }
    }

    /// <summary>
    /// Execute all commands scheduled for the given tick.
    /// </summary>
    public void ExecuteCommands(ulong currentTick, ISimulationContext context)
    {
        lock (_executeLock)
        {
            ArgumentNullException.ThrowIfNull(context);

            var commandsToExecute = new List<QueuedCommand>();
            var futureCommands = new List<QueuedCommand>();

            // Collect all visible commands. A future command must not block a later
            // due command that was enqueued out of order by UI or replay restore.
            while (_pendingCommands.TryDequeue(out var queued))
            {
                if (queued.Command.Tick <= currentTick)
                {
                    commandsToExecute.Add(queued);
                }
                else
                {
                    futureCommands.Add(queued);
                }
            }

            commandsToExecute.Sort(CompareQueuedCommands);
            futureCommands.Sort((a, b) =>
            {
                var tickCompare = a.Command.Tick.CompareTo(b.Command.Tick);
                return tickCompare != 0 ? tickCompare : a.Sequence.CompareTo(b.Sequence);
            });

            foreach (var command in futureCommands)
            {
                _pendingCommands.Enqueue(command);
            }

            // Execute in deterministic order
            foreach (var queued in commandsToExecute)
            {
                var command = queued.Command;
                try
                {
                    command.Execute(context);
                    _executedCommands.Add(command);
                }
                catch (Exception ex)
                {
                    HandleCommandError(queued, ex);
                }
            }
        }
    }

    /// <summary>
    /// Get all executed command objects for in-memory diagnostics and compatibility.
    /// Use GetExecutedCommandRecords for replay/save persistence.
    /// </summary>
    public IReadOnlyList<ICommand> GetExecutedCommands()
    {
        lock (_executeLock)
        {
            return _executedCommands.ToList();
        }
    }

    /// <summary>
    /// Get immutable command replay records for save/replay persistence.
    /// </summary>
    public IReadOnlyList<CommandReplayRecord> GetExecutedCommandRecords()
    {
        lock (_executeLock)
        {
            return CaptureExecutedCommandRecordsNoLock();
        }
    }

    /// <summary>
    /// Get immutable pending command replay records in their future execution order.
    /// </summary>
    public IReadOnlyList<CommandReplayRecord> GetPendingCommandRecords()
    {
        lock (_executeLock)
        {
            return CapturePendingCommandRecordsNoLock();
        }
    }

    /// <summary>
    /// Get a single replay/save snapshot of both pending and executed command records.
    /// </summary>
    public CommandQueueReplaySnapshot GetReplaySnapshot()
    {
        lock (_executeLock)
        {
            return new CommandQueueReplaySnapshot(
                CapturePendingCommandRecordsNoLock(),
                CaptureExecutedCommandRecordsNoLock());
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
    /// Clear all pending and executed commands for a new simulation session.
    /// </summary>
    public void Clear()
    {
        lock (_executeLock)
        {
            _executedCommands.Clear();
            while (_pendingCommands.TryDequeue(out _))
            {
            }

            _nextSequence = 0;
        }
    }

    /// <summary>
    /// Restore pending commands from save/replay without marking them executed.
    /// </summary>
    public void RestoreCommands(IEnumerable<ICommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var restoredCommands = commands.ToList();
        foreach (var command in restoredCommands)
        {
            ArgumentNullException.ThrowIfNull(command);
        }

        lock (_executeLock)
        {
            _executedCommands.Clear();
            while (_pendingCommands.TryDequeue(out _))
            {
            }

            _nextSequence = 0;
            foreach (var cmd in restoredCommands)
            {
                var sequence = ++_nextSequence;
                _pendingCommands.Enqueue(new QueuedCommand(cmd, sequence));
            }
        }
    }

    private static int CompareQueuedCommands(QueuedCommand a, QueuedCommand b)
    {
        var tickCompare = a.Command.Tick.CompareTo(b.Command.Tick);
        return tickCompare != 0 ? tickCompare : a.Sequence.CompareTo(b.Sequence);
    }

    private IReadOnlyList<CommandReplayRecord> CapturePendingCommandRecordsNoLock()
    {
        return _pendingCommands
            .ToArray()
            .OrderBy(queued => queued.Command.Tick)
            .ThenBy(queued => queued.Sequence)
            .Select(queued => CommandReplayRecord.FromCommand(queued.Command))
            .ToList();
    }

    private IReadOnlyList<CommandReplayRecord> CaptureExecutedCommandRecordsNoLock()
    {
        return _executedCommands
            .Select(CommandReplayRecord.FromCommand)
            .ToList();
    }

    private void HandleCommandError(QueuedCommand queued, Exception ex)
    {
        // Per ERROR_HANDLING_POLICY.md: log and continue
        var command = queued.Command;
        DiagnosticHub.Error(
            "Core.CommandQueue",
            $"[ERROR] Command {command.CommandType} ({command.CommandId}, seq={queued.Sequence}) failed: {ex.Message}",
            ex,
            command.Tick);
    }

    private readonly record struct QueuedCommand(ICommand Command, long Sequence);
}
