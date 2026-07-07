using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeDebugSpawnCommandAccess
{
    void QueueCreatureSpawn(string creatureId, Point position, int z, string factionId);

    void QueueItemSpawn(string itemId, Point position, int z, int quantity = 1);
}
