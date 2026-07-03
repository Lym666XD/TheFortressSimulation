using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Command that creates a mining designation over a world-space rectangle at a given Z.
/// Executed through the simulation tick command stage.
/// </summary>
internal sealed class CreateMiningOrderCommand : ICommand
{
    internal ulong Tick { get; }
    private Guid CommandId => RuntimeCommandId.Create(this);
    private string CommandType => "orders.mining.rect";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    private readonly Rectangle _worldRect;
    private readonly int _z;
    private readonly int _priority;

    internal CreateMiningOrderCommand(ulong tick, Rectangle worldRect, int z, int priority = 50)
    {
        Tick = tick;
        _worldRect = worldRect;
        _z = z;
        _priority = priority;
    }

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeOrderCommandTargetContext>(context, CommandType);

        runtimeContext.Orders.EnqueueMiningOrder(_worldRect, _z, _priority, context.CurrentTick);
    }

    byte[] ICommand.Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = RuntimeCommandPayload.CreateWriter(ms);
        bw.Write((int)_worldRect.X);
        bw.Write((int)_worldRect.Y);
        bw.Write((int)_worldRect.Width);
        bw.Write((int)_worldRect.Height);
        bw.Write(_z);
        bw.Write(_priority);
        return ms.ToArray();
    }
}
