using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime.Commands;

internal sealed class RuntimeCommandReplayRestorer
{
    private readonly ICommandReplayFactory _factory;

    internal RuntimeCommandReplayRestorer()
        : this(new RuntimeCommandReplayFactory())
    {
    }

    internal RuntimeCommandReplayRestorer(ICommandReplayFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    internal RuntimeCommandReplayRestoreResult RestorePending(
        RuntimeSessionServices services,
        IEnumerable<CommandReplayRecord> records)
    {
        ArgumentNullException.ThrowIfNull(services);

        return RestorePendingCore(
            services.CommandQueue,
            records,
            maxCommandIdentitySequence => services.AdvanceCommandIdentitySequenceTo(maxCommandIdentitySequence));
    }

    internal RuntimeCommandReplayRestoreResult RestorePending(
        CommandQueue commandQueue,
        IEnumerable<CommandReplayRecord> records)
    {
        return RestorePendingCore(commandQueue, records, afterRestore: null);
    }

    private RuntimeCommandReplayRestoreResult RestorePendingCore(
        CommandQueue commandQueue,
        IEnumerable<CommandReplayRecord> records,
        Action<long>? afterRestore)
    {
        ArgumentNullException.ThrowIfNull(commandQueue);
        ArgumentNullException.ThrowIfNull(records);

        var decodedCommands = new List<ICommand>();
        var issues = new List<RuntimeCommandReplayRestoreIssue>();
        var recordCount = 0;
        var maxCommandIdentitySequence = 0L;

        foreach (var record in records)
        {
            var index = recordCount++;
            if (record == null)
            {
                issues.Add(new RuntimeCommandReplayRestoreIssue(index, "<null>", "Replay record is null."));
                continue;
            }

            if (record.CommandIdentitySequence is { } identitySequence)
                maxCommandIdentitySequence = Math.Max(maxCommandIdentitySequence, identitySequence);

            try
            {
                if (!_factory.TryCreateCommand(record, out var command, out var errorMessage) || command == null)
                {
                    issues.Add(new RuntimeCommandReplayRestoreIssue(
                        index,
                        record.CommandType,
                        errorMessage ?? "Replay command factory did not return a command."));
                    continue;
                }

                decodedCommands.Add(command);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                issues.Add(new RuntimeCommandReplayRestoreIssue(
                    index,
                    record.CommandType,
                    ex.Message));
            }
        }

        if (issues.Count > 0)
            return RuntimeCommandReplayRestoreResult.Failed(recordCount, issues);

        commandQueue.RestoreCommands(decodedCommands);
        afterRestore?.Invoke(maxCommandIdentitySequence);

        return RuntimeCommandReplayRestoreResult.Succeeded(
            recordCount,
            decodedCommands.Count,
            maxCommandIdentitySequence);
    }
}
