using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Command to delete a zone entirely.
/// </summary>
internal sealed class DeleteZoneCommand : ICommand
{
    internal ulong Tick { get; }
    private Guid CommandId => RuntimeCommandId.Create(this);
    private string CommandType => "zones.delete";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    private readonly int _zoneId;

    internal DeleteZoneCommand(ulong tick, int zoneId)
    {
        Tick = tick;
        _zoneId = zoneId;
    }

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeZoneCommandTargetContext>(context, CommandType);

        runtimeContext.Zones.DeleteZone(_zoneId);
    }

    byte[] ICommand.Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(_zoneId);
        return ms.ToArray();
    }
}
