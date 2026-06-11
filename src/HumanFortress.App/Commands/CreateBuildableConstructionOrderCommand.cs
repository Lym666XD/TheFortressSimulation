using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Commands;

/// <summary>
/// Command that enqueues a buildable construction (L2 placeable) at an anchor cell.
/// </summary>
public sealed class CreateBuildableConstructionOrderCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "orders.construction.buildable.anchor";

    private readonly string _constructionId;
    private readonly Point _anchor;
    private readonly int _z;
    private readonly int _priority;

    public CreateBuildableConstructionOrderCommand(ulong tick, string constructionId, Point anchor, int z, int priority = 50)
    {
        Tick = tick;
        _constructionId = constructionId;
        _anchor = anchor;
        _z = z;
        _priority = priority;
    }

    public void Execute(ISimulationContext context)
    {
        if (context is IOrderCommandTarget target)
        {
            target.EnqueueBuildableConstructionOrder(_constructionId, _anchor, _z, _priority, context.CurrentTick);
        }
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(_constructionId);
        bw.Write(_anchor.X);
        bw.Write(_anchor.Y);
        bw.Write(_z);
        bw.Write(_priority);
        return ms.ToArray();
    }
}
