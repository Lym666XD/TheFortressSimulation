using HumanFortress.Contracts.Runtime;
using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Geometry;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    void IFortressRuntimeSessionDebugCommandPort.QueueCreatureSpawn(
        string creatureId,
        RuntimePoint position,
        int z,
        string factionId)
    {
        EnqueueCurrentTickCommand(RuntimeDebugCommandFactory.CreateSpawnCreature(
            creatureId,
            position.ToSadRoguePoint(),
            z,
            factionId));
    }

    void IFortressRuntimeSessionDebugCommandPort.QueueItemSpawn(string itemId, RuntimePoint position, int z, int quantity)
    {
        EnqueueCurrentTickCommand(RuntimeDebugCommandFactory.CreateSpawnItem(
            itemId,
            position.ToSadRoguePoint(),
            z,
            quantity));
    }
}
