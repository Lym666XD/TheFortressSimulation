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
internal sealed class CreateStockpileCommand : ICommand
{
    internal ulong Tick { get; }
    private Guid CommandId => RuntimeCommandId.Create(this);
    private string CommandType => "stockpiles.create";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    private readonly Rectangle _worldRect;
    private readonly int _z;
    private readonly string _presetId;

    internal CreateStockpileCommand(ulong tick, Rectangle worldRect, int z, string presetId)
    {
        Tick = tick;
        _worldRect = worldRect;
        _z = z;
        _presetId = string.IsNullOrWhiteSpace(presetId) ? "all" : presetId;
    }

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeStockpileCommandTargetContext>(context, CommandType);

        runtimeContext.Stockpiles.CreateStockpile(_worldRect, _z, _presetId, context.CurrentTick);
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
        bw.Write(_presetId);
        return ms.ToArray();
    }
}
