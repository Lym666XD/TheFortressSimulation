using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Command to add or remove cells from an existing zone.
/// </summary>
public sealed class UpdateZoneCellsCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "zones.update_cells";

    private readonly int _zoneId;
    private readonly Rectangle _worldRect;
    private readonly int _z;
    private readonly bool _isAdding; // true = add, false = remove

    public UpdateZoneCellsCommand(ulong tick, int zoneId, Rectangle worldRect, int z, bool isAdding)
    {
        Tick = tick;
        _zoneId = zoneId;
        _worldRect = worldRect;
        _z = z;
        _isAdding = isAdding;
    }

    public void Execute(ISimulationContext context)
    {
        if (context is IZoneCommandTarget target)
        {
            if (_isAdding)
            {
                target.AddZoneCells(_zoneId, _worldRect, _z);
            }
            else
            {
                target.RemoveZoneCells(_zoneId, _worldRect, _z);
            }
        }
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(_zoneId);
        bw.Write((int)_worldRect.X);
        bw.Write((int)_worldRect.Y);
        bw.Write((int)_worldRect.Width);
        bw.Write((int)_worldRect.Height);
        bw.Write(_z);
        bw.Write(_isAdding);
        return ms.ToArray();
    }
}
