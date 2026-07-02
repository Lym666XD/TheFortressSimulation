using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;

namespace HumanFortress.Runtime.Commands;

internal sealed class RuntimeIdentifiedCommand : ICommand
{
    private readonly ICommand _inner;
    private readonly Guid _commandId;

    internal RuntimeIdentifiedCommand(ICommand inner, long sequence)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _commandId = RuntimeCommandId.Create(inner, sequence);
    }

    ulong ICommand.Tick => _inner.Tick;

    Guid ICommand.CommandId => _commandId;

    string ICommand.CommandType => _inner.CommandType;

    void ICommand.Execute(ISimulationContext context)
    {
        _inner.Execute(context);
    }

    byte[] ICommand.Serialize()
    {
        return _inner.Serialize();
    }
}
