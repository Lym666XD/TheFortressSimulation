using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;

namespace HumanFortress.Runtime.Commands;

internal sealed class RuntimeIdentifiedCommand : ICommand, ICommandReplayIdentity
{
    private readonly ICommand _inner;
    private readonly Guid _commandId;
    private readonly long _sequence;

    internal RuntimeIdentifiedCommand(ICommand inner, long sequence)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _commandId = RuntimeCommandId.Create(inner, sequence);
        _sequence = sequence;
    }

    ulong ICommand.Tick => _inner.Tick;

    Guid ICommand.CommandId => _commandId;

    string ICommand.CommandType => _inner.CommandType;

    long? ICommandReplayIdentity.CommandIdentitySequence => _sequence;

    void ICommand.Execute(ISimulationContext context)
    {
        _inner.Execute(context);
    }

    byte[] ICommand.Serialize()
    {
        return _inner.Serialize();
    }
}
