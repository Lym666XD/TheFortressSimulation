using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Debug command for spawning an item through the simulation command boundary.
/// </summary>
internal sealed class SpawnItemCommand : ICommand
{
    internal ulong Tick { get; }
    private Guid CommandId => RuntimeCommandId.Create(this);
    private string CommandType => "debug.spawn.item";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    private readonly string _itemId;
    private readonly Point _worldPos;
    private readonly int _z;
    private readonly int _quantity;

    internal SpawnItemCommand(ulong tick, string itemId, Point worldPos, int z, int quantity = 1)
    {
        Tick = tick;
        _itemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
        _worldPos = worldPos;
        _z = z;
        _quantity = Math.Max(1, quantity);
    }

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeItemSpawnCommandTargetContext>(context, CommandType);

        runtimeContext.Items.AddItemSpawn(_itemId, _worldPos, _z, _quantity);
    }

    byte[] ICommand.Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = RuntimeCommandPayload.CreateWriter(ms);
        bw.Write(_itemId);
        bw.Write((int)_worldPos.X);
        bw.Write((int)_worldPos.Y);
        bw.Write(_z);
        bw.Write(_quantity);
        return ms.ToArray();
    }
}
