using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeDebugSpawnAccess
{
    SimulationDebugSpawnData GetDebugSpawnData();
    void QueueCreatureSpawn(string creatureId, Point position, int z, string factionId);
    void QueueItemSpawn(string itemId, Point position, int z, int quantity = 1);
}
