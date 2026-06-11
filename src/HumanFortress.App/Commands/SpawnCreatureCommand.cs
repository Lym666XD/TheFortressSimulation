using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Commands;

/// <summary>
/// Debug command for spawning a creature through the simulation command boundary.
/// </summary>
public sealed class SpawnCreatureCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "debug.spawn.creature";

    private readonly string _creatureId;
    private readonly Point _worldPos;
    private readonly int _z;
    private readonly string _factionId;

    public SpawnCreatureCommand(ulong tick, string creatureId, Point worldPos, int z, string factionId = "neutral")
    {
        Tick = tick;
        _creatureId = creatureId ?? throw new ArgumentNullException(nameof(creatureId));
        _worldPos = worldPos;
        _z = z;
        _factionId = string.IsNullOrWhiteSpace(factionId) ? "neutral" : factionId;
    }

    public void Execute(ISimulationContext context)
    {
        if (context is ICreatureSpawnCommandTarget target && target.AddCreatureSpawn(_creatureId, _worldPos, _z, _factionId))
        {
            Logger.Log($"[DEBUG] QUEUED: Spawn creature '{_creatureId}' at ({_worldPos.X},{_worldPos.Y},{_z})");
        }
        else
        {
            Logger.Log($"[DEBUG] FAILED: Could not queue creature spawn '{_creatureId}' at ({_worldPos.X},{_worldPos.Y},{_z})");
        }
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(_creatureId);
        bw.Write((int)_worldPos.X);
        bw.Write((int)_worldPos.Y);
        bw.Write(_z);
        bw.Write(_factionId);
        return ms.ToArray();
    }
}
