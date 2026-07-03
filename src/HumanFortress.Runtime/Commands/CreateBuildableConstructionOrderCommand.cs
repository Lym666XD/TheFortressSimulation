using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Command that enqueues a buildable construction (L2 placeable) at an anchor cell.
/// </summary>
internal sealed class CreateBuildableConstructionOrderCommand : ICommand
{
    internal ulong Tick { get; }
    private Guid CommandId => RuntimeCommandId.Create(this);
    private string CommandType => "orders.construction.buildable.anchor";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    private readonly string _constructionId;
    private readonly Point _anchor;
    private readonly int _z;
    private readonly int _priority;

    internal CreateBuildableConstructionOrderCommand(ulong tick, string constructionId, Point anchor, int z, int priority = 50)
    {
        Tick = tick;
        _constructionId = constructionId;
        _anchor = anchor;
        _z = z;
        _priority = priority;
    }

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeOrderCommandTargetContext>(context, CommandType);

        runtimeContext.Orders.EnqueueBuildableConstructionOrder(
            _constructionId,
            _anchor,
            _z,
            _priority,
            context.CurrentTick);
    }

    byte[] ICommand.Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = RuntimeCommandPayload.CreateWriter(ms);
        bw.Write(_constructionId);
        bw.Write(_anchor.X);
        bw.Write(_anchor.Y);
        bw.Write(_z);
        bw.Write(_priority);
        return ms.ToArray();
    }
}
