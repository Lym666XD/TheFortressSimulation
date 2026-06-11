using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Orders;
using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Commands;

/// <summary>
/// Command that creates an advanced mining designation with z-range and mining action.
/// For now, OrdersManager will decompose DIG/DIG_RAMP into per-Z MiningDesignation.
/// Other actions are queued for future handling.
/// </summary>
public sealed class CreateAdvancedMiningOrderCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "orders.mining.advanced_rect";

    private readonly Rectangle _worldRect;
    private readonly int _zMin;
    private readonly int _zMax;
    private readonly UI.MiningAction _action;
    private readonly int _priority;

    public CreateAdvancedMiningOrderCommand(ulong tick, Rectangle worldRect, int zMin, int zMax, UI.MiningAction action, int priority = 50)
    {
        Tick = tick;
        _worldRect = worldRect;
        _zMin = Math.Min(zMin, zMax);
        _zMax = Math.Max(zMin, zMax);
        _action = action;
        _priority = priority;
    }

    public void Execute(ISimulationContext context)
    {
        if (context is IOrderCommandTarget target)
        {
            var act = _action switch
            {
                UI.MiningAction.Dig => HumanFortress.Simulation.Orders.MiningAction.Dig,
                UI.MiningAction.DigStairwell => HumanFortress.Simulation.Orders.MiningAction.DigStairwell,
                UI.MiningAction.DigRamp => HumanFortress.Simulation.Orders.MiningAction.DigRamp,
                UI.MiningAction.DigChannel => HumanFortress.Simulation.Orders.MiningAction.DigChannel,
                UI.MiningAction.RemoveDigging => HumanFortress.Simulation.Orders.MiningAction.RemoveDigging,
                _ => HumanFortress.Simulation.Orders.MiningAction.Dig
            };
            // Always enqueue; planner will skip if nothing eligible. This avoids false negatives at UI boundary.
            target.EnqueueAdvancedMiningOrder(_worldRect, _zMin, _zMax, act, _priority, context.CurrentTick);
        }
    }

    public byte[] Serialize()
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
