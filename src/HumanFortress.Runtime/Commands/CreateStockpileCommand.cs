using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Command to create a stockpile zone and its per-chunk stockpile cell shards.
/// </summary>
public sealed class CreateStockpileCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "stockpiles.create";

    private readonly Rectangle _worldRect;
    private readonly int _z;
    private readonly string _presetId;

    public CreateStockpileCommand(ulong tick, Rectangle worldRect, int z, string presetId)
    {
        Tick = tick;
        _worldRect = worldRect;
        _z = z;
        _presetId = string.IsNullOrWhiteSpace(presetId) ? "all" : presetId;
    }

    public void Execute(ISimulationContext context)
    {
        if (context is IStockpileCommandTarget target)
            target.CreateStockpile(_worldRect, _z, _presetId, context.CurrentTick);
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
        bw.Write(_presetId);
        return ms.ToArray();
    }
}
