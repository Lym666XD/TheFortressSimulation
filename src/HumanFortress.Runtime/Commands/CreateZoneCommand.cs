using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Command to create a new zone with specified cells.
/// Executed through the simulation tick command stage.
/// </summary>
internal sealed class CreateZoneCommand : ICommand
{
    internal ulong Tick { get; }
    private Guid CommandId => RuntimeCommandId.Create(this);
    private string CommandType => "zones.create";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    private readonly string _defId;
    private readonly string _name;
    private readonly Rectangle _worldRect;
    private readonly int _z;

    internal CreateZoneCommand(ulong tick, string defId, string name, Rectangle worldRect, int z)
    {
        Tick = tick;
        _defId = defId ?? throw new ArgumentNullException(nameof(defId));
        _name = name ?? $"Zone";
        _worldRect = worldRect;
        _z = z;
    }

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeZoneCommandTargetContext>(context, CommandType);

        runtimeContext.Zones.CreateZone(_defId, _name, _worldRect, _z, context.CurrentTick);
    }

    byte[] ICommand.Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = RuntimeCommandPayload.CreateWriter(ms);
        bw.Write(_defId);
        bw.Write(_name);
        bw.Write((int)_worldRect.X);
        bw.Write((int)_worldRect.Y);
        bw.Write((int)_worldRect.Width);
        bw.Write((int)_worldRect.Height);
        bw.Write(_z);
        return ms.ToArray();
    }
}
