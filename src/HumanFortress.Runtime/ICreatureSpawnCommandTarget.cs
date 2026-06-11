using SadRogue.Primitives;

namespace HumanFortress.Runtime;

public interface ICreatureSpawnCommandTarget
{
    bool AddCreatureSpawn(string creatureId, Point worldPos, int z, string factionId);
}
