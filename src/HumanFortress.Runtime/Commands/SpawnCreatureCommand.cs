using System;
using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Debug command for spawning a creature through the simulation command boundary.
/// </summary>
internal sealed class SpawnCreatureCommand : ICommand
{
    internal ulong Tick { get; }
    private Guid CommandId => RuntimeCommandId.Create(this);
    private string CommandType => "debug.spawn.creature";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    private readonly string _creatureId;
    private readonly Point _worldPos;
    private readonly int _z;
    private readonly string _factionId;

    internal SpawnCreatureCommand(ulong tick, string creatureId, Point worldPos, int z, string factionId = "neutral")
    {
        Tick = tick;
        _creatureId = creatureId ?? throw new ArgumentNullException(nameof(creatureId));
        _worldPos = worldPos;
        _z = z;
        _factionId = string.IsNullOrWhiteSpace(factionId) ? "neutral" : factionId;
    }

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeCreatureSpawnCommandTargetContext>(context, CommandType);

        runtimeContext.Creatures.AddCreatureSpawn(_creatureId, _worldPos, _z, _factionId);
    }

    byte[] ICommand.Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = RuntimeCommandPayload.CreateWriter(ms);
        bw.Write(_creatureId);
        bw.Write((int)_worldPos.X);
        bw.Write((int)_worldPos.Y);
        bw.Write(_z);
        bw.Write(_factionId);
        return ms.ToArray();
    }
}
