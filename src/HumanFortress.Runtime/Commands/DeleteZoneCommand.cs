using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Command to delete a zone entirely.
/// </summary>
public sealed class DeleteZoneCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "zones.delete";

    private readonly int _zoneId;

    public DeleteZoneCommand(ulong tick, int zoneId)
    {
        Tick = tick;
        _zoneId = zoneId;
    }

    public void Execute(ISimulationContext context)
    {
        if (context is IZoneCommandTarget target)
        {
            target.DeleteZone(_zoneId);
        }
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(_zoneId);
        return ms.ToArray();
    }
}
