using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Command that creates an advanced mining designation with z-range and mining action.
/// For now, OrdersManager will decompose DIG/DIG_RAMP into per-Z MiningDesignation.
/// Other actions are queued for future handling.
/// </summary>
internal sealed class CreateAdvancedMiningOrderCommand : ICommand
{
    internal ulong Tick { get; }
    private Guid CommandId { get; } = Guid.NewGuid();
    private string CommandType => "orders.mining.advanced_rect";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    private readonly Rectangle _worldRect;
    private readonly int _zMin;
    private readonly int _zMax;
    private readonly MiningAction _action;
    private readonly int _priority;

    internal CreateAdvancedMiningOrderCommand(ulong tick, Rectangle worldRect, int zMin, int zMax, MiningAction action, int priority = 50)
    {
        Tick = tick;
        _worldRect = worldRect;
        _zMin = Math.Min(zMin, zMax);
        _zMax = Math.Max(zMin, zMax);
        _action = action;
        _priority = priority;
    }

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeOrderCommandTargetContext>(context, CommandType);

        // Always enqueue; planner will skip if nothing eligible. This avoids false negatives at UI boundary.
        runtimeContext.Orders.EnqueueAdvancedMiningOrder(
            _worldRect,
            _zMin,
            _zMax,
            _action,
            _priority,
            context.CurrentTick);
    }

    byte[] ICommand.Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write((int)_worldRect.X);
        bw.Write((int)_worldRect.Y);
        bw.Write((int)_worldRect.Width);
        bw.Write((int)_worldRect.Height);
        bw.Write(_zMin);
        bw.Write(_zMax);
        bw.Write((byte)_action);
        bw.Write(_priority);
        return ms.ToArray();
    }
}
