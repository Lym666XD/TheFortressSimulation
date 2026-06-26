using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal void QueueCreatureSpawn(string creatureId, Point position, int z, string factionId)
    {
        _debugCommands.QueueCreatureSpawn(
            creatureId,
            position.ToRuntimePoint(),
            z,
            factionId);
    }

    internal void QueueItemSpawn(string itemId, Point position, int z, int quantity = 1)
    {
        _debugCommands.QueueItemSpawn(
            itemId,
            position.ToRuntimePoint(),
            z,
            quantity);
    }

    void IFortressRuntimeDebugSpawnAccess.QueueCreatureSpawn(string creatureId, Point position, int z, string factionId) =>
        QueueCreatureSpawn(creatureId, position, z, factionId);

    void IFortressRuntimeDebugSpawnAccess.QueueItemSpawn(string itemId, Point position, int z, int quantity) =>
        QueueItemSpawn(itemId, position, z, quantity);
}
