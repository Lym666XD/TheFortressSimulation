using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Command to add or remove cells from an existing zone.
/// </summary>
internal sealed class UpdateZoneCellsCommand : ICommand
{
    internal ulong Tick { get; }
    private Guid CommandId => RuntimeCommandId.Create(this);
    private string CommandType => "zones.update_cells";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    private readonly int _zoneId;
    private readonly Rectangle _worldRect;
    private readonly int _z;
    private readonly bool _isAdding; // true = add, false = remove

    internal UpdateZoneCellsCommand(ulong tick, int zoneId, Rectangle worldRect, int z, bool isAdding)
    {
        Tick = tick;
        _zoneId = zoneId;
        _worldRect = worldRect;
        _z = z;
        _isAdding = isAdding;
    }

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeZoneCommandTargetContext>(context, CommandType);

        if (_isAdding)
        {
            runtimeContext.Zones.AddZoneCells(_zoneId, _worldRect, _z);
        }
        else
        {
            runtimeContext.Zones.RemoveZoneCells(_zoneId, _worldRect, _z);
        }
    }

    byte[] ICommand.Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = RuntimeCommandPayload.CreateWriter(ms);
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
