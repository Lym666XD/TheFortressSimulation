using System.Collections.ObjectModel;

namespace HumanFortress.Core.Commands;

public sealed class CommandQueueReplaySnapshot
{
    private readonly ReadOnlyCollection<CommandReplayRecord> _pendingRecords;
    private readonly ReadOnlyCollection<CommandReplayRecord> _executedRecords;

    public CommandQueueReplaySnapshot(
        IEnumerable<CommandReplayRecord> pendingRecords,
        IEnumerable<CommandReplayRecord> executedRecords)
    {
        ArgumentNullException.ThrowIfNull(pendingRecords);
        ArgumentNullException.ThrowIfNull(executedRecords);

        var pending = pendingRecords.ToArray();
        var executed = executedRecords.ToArray();
        foreach (var record in pending)
        {
            ArgumentNullException.ThrowIfNull(record);
        }

        foreach (var record in executed)
        {
            ArgumentNullException.ThrowIfNull(record);
        }

        _pendingRecords = Array.AsReadOnly(pending);
        _executedRecords = Array.AsReadOnly(executed);
    }

    public IReadOnlyList<CommandReplayRecord> PendingRecords => _pendingRecords;
    public IReadOnlyList<CommandReplayRecord> ExecutedRecords => _executedRecords;
}
