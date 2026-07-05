using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal sealed class CreatureSpawnCommandTarget : ICreatureSpawnCommandTarget
{
    private readonly World _world;
    private readonly CreaturesDiffLog _creaturesDiffLog;

    internal CreatureSpawnCommandTarget(World world, CreaturesDiffLog creaturesDiffLog)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _creaturesDiffLog = creaturesDiffLog ?? throw new ArgumentNullException(nameof(creaturesDiffLog));
    }

    bool ICreatureSpawnCommandTarget.AddCreatureSpawn(string creatureId, Point worldPos, int z, string factionId)
    {
        if (string.IsNullOrWhiteSpace(creatureId)) return false;
        if (!IsValidSpawnTarget(worldPos, z)) return false;

        _creaturesDiffLog.AddSpawnCreature(creatureId, worldPos, z, factionId, priority: 100, systemId: "Commands.SpawnCreature");
        return true;
    }

    private bool IsValidSpawnTarget(Point worldPos, int z)
    {
        if (worldPos.X < 0 || worldPos.Y < 0 || z < 0) return false;
        return _world.IsValidPosition(worldPos.X, worldPos.Y, z);
    }
}
