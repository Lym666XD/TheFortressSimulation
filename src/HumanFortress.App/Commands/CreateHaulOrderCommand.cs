using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Commands;

/// <summary>
/// Command that creates a haul designation over a world-space rectangle at a given Z.
/// Executed on the main thread; OrdersManager is thread-safe for enqueue.
/// </summary>
public sealed class CreateHaulOrderCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "orders.haul.rect";

    private readonly Rectangle _worldRect;
    private readonly int _z;
    private readonly int _priority;

    public CreateHaulOrderCommand(ulong tick, Rectangle worldRect, int z, int priority = 50)
    {
        Tick = tick;
        _worldRect = worldRect;
        _z = z;
        _priority = priority;
    }

    public void Execute(ISimulationContext context)
    {
        if (context.World is World world)
        {
            world.Orders.EnqueueHaul(_worldRect, _z, _priority, context.CurrentTick);
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
        bw.Write(_z);
        bw.Write(_priority);
        return ms.ToArray();
    }
}

