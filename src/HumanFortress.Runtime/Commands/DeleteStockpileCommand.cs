using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;

namespace HumanFortress.Runtime.Commands;

internal sealed class DeleteStockpileCommand : ICommand
{
    internal DeleteStockpileCommand(ulong tick, int zoneId)
    {
        Tick = tick;
        ZoneId = zoneId;
    }

    internal ulong Tick { get; }
    private int ZoneId { get; }
    private Guid CommandId => RuntimeCommandId.Create(this);
    private string CommandType => "stockpiles.delete";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeStockpileCommandTargetContext>(context, CommandType);
        runtimeContext.Stockpiles.DeleteStockpile(ZoneId, context.CurrentTick);
    }

    byte[] ICommand.Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(ZoneId);
        return ms.ToArray();
    }
}
