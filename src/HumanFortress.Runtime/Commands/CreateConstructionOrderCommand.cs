using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Command that creates a construction designation (L0 structural) over a world-space rectangle across z-range.
/// Executed through the simulation tick command stage.
/// </summary>
internal sealed class CreateConstructionOrderCommand : ICommand
{
    internal ulong Tick { get; }
    private Guid CommandId => RuntimeCommandId.Create(this);
    private string CommandType => "orders.construction.rect";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    private readonly Rectangle _worldRect;
    private readonly int _zMin;
    private readonly int _zMax;
    private readonly ConstructionShape _shape;
    private readonly MaterialFilterSpec _filter;
    private readonly int _priority;

    internal CreateConstructionOrderCommand(ulong tick, Rectangle worldRect, int zMin, int zMax, ConstructionShape shape, MaterialFilterSpec filter, int priority = 50)
    {
        Tick = tick;
        _worldRect = worldRect;
        _zMin = Math.Min(zMin, zMax);
        _zMax = Math.Max(zMin, zMax);
        _shape = shape;
        _filter = filter;
        _priority = priority;
    }

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeOrderCommandTargetContext>(context, CommandType);

        runtimeContext.Orders.EnqueueConstructionOrder(
            _worldRect,
            _zMin,
            _zMax,
            _shape,
            _filter,
            _priority,
            context.CurrentTick);
    }

    byte[] ICommand.Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = RuntimeCommandPayload.CreateWriter(ms);
        bw.Write((int)_worldRect.X);
        bw.Write((int)_worldRect.Y);
        bw.Write((int)_worldRect.Width);
        bw.Write((int)_worldRect.Height);
        bw.Write(_zMin);
        bw.Write(_zMax);
        bw.Write((byte)_shape);
        bw.Write(_filter.PreferredMaterialId ?? string.Empty);
        bw.Write(_filter.CategoryKey ?? string.Empty);
        var tags = _filter.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Order(StringComparer.Ordinal)
            .ToArray();
        bw.Write(tags.Length);
        foreach (var tag in tags)
        {
            bw.Write(tag);
        }

        bw.Write(_priority);
        return ms.ToArray();
    }
}
