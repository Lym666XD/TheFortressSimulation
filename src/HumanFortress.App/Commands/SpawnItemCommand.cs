using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Commands;

/// <summary>
/// Debug command for spawning an item through the simulation command boundary.
/// </summary>
public sealed class SpawnItemCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "debug.spawn.item";

    private readonly string _itemId;
    private readonly Point _worldPos;
    private readonly int _z;
    private readonly int _quantity;

    public SpawnItemCommand(ulong tick, string itemId, Point worldPos, int z, int quantity = 1)
    {
        Tick = tick;
        _itemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
        _worldPos = worldPos;
        _z = z;
        _quantity = Math.Max(1, quantity);
    }

    public void Execute(ISimulationContext context)
    {
        if (context.World is not World world)
            return;

        var guid = world.Items.SpawnItem(_itemId, _worldPos, _z, _quantity, context.CurrentTick);
        if (guid.HasValue)
        {
            Logger.Log($"[DEBUG] SUCCESS: Spawned item '{_itemId}' guid={guid.Value} qty={_quantity} at ({_worldPos.X},{_worldPos.Y},{_z})");
        }
        else
        {
            Logger.Log($"[DEBUG] FAILED: Could not spawn item '{_itemId}' at ({_worldPos.X},{_worldPos.Y},{_z})");
        }
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(_itemId);
        bw.Write((int)_worldPos.X);
        bw.Write((int)_worldPos.Y);
        bw.Write(_z);
        bw.Write(_quantity);
        return ms.ToArray();
    }
}
