using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Commands;

/// <summary>
/// Command to create a new zone with specified cells.
/// Executed through the simulation tick command stage.
/// </summary>
public sealed class CreateZoneCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "zones.create";

    private readonly string _defId;
    private readonly string _name;
    private readonly Rectangle _worldRect;
    private readonly int _z;

    public CreateZoneCommand(ulong tick, string defId, string name, Rectangle worldRect, int z)
    {
        Tick = tick;
        _defId = defId ?? throw new ArgumentNullException(nameof(defId));
        _name = name ?? $"Zone";
        _worldRect = worldRect;
        _z = z;
    }

    public void Execute(ISimulationContext context)
    {
        if (context is IZoneCommandTarget target)
        {
            target.CreateZone(_defId, _name, _worldRect, _z, context.CurrentTick);
        }
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
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
