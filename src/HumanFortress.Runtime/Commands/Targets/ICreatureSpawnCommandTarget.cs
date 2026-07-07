using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal interface ICreatureSpawnCommandTarget
{
    bool AddCreatureSpawn(string creatureId, Point worldPos, int z, string factionId);
}
