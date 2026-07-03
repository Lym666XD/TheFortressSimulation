namespace HumanFortress.Core.Commands;

/// <summary>
/// Immutable replay/save boundary for a command that has executed.
/// </summary>
public sealed class CommandReplayRecord
{
    private readonly byte[] _payload;

    public CommandReplayRecord(
        ulong tick,
        Guid commandId,
        string commandType,
        ReadOnlyMemory<byte> payload,
        long? commandIdentitySequence = null)
    {
        if (string.IsNullOrWhiteSpace(commandType))
            throw new ArgumentException("Command type must not be blank.", nameof(commandType));
        if (commandIdentitySequence.HasValue && commandIdentitySequence.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(commandIdentitySequence), "Command identity sequence must be positive.");

        Tick = tick;
        CommandId = commandId;
        CommandType = commandType;
        _payload = payload.ToArray();
        CommandIdentitySequence = commandIdentitySequence;
    }

    public ulong Tick { get; }
    public Guid CommandId { get; }
    public string CommandType { get; }
    public ReadOnlyMemory<byte> Payload => _payload;
    public int PayloadLength => _payload.Length;
    public long? CommandIdentitySequence { get; }

    public static CommandReplayRecord FromCommand(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return new CommandReplayRecord(
            command.Tick,
            command.CommandId,
            command.CommandType,
            command.Serialize(),
            command is ICommandReplayIdentity replayIdentity
                ? replayIdentity.CommandIdentitySequence
                : null);
    }

    public byte[] ToPayloadArray()
    {
        return _payload.ToArray();
    }
}
