using SadRogue.Primitives;

namespace HumanFortress.Runtime;

internal interface ICreatureSpawnCommandTarget
{
    bool AddCreatureSpawn(string creatureId, Point worldPos, int z, string factionId);
}
