using HumanFortress.Core.Commands;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal static class RuntimeDebugCommandFactory
{
    internal static Func<ulong, ICommand> CreateSpawnCreature(
        string creatureId,
        Point position,
        int z,
        string factionId)
    {
        return tick => new SpawnCreatureCommand(
            tick,
            creatureId,
            position,
            z,
            factionId);
    }

    internal static Func<ulong, ICommand> CreateSpawnItem(
        string itemId,
        Point position,
        int z,
        int quantity = 1)
    {
        return tick => new SpawnItemCommand(
            tick,
            itemId,
            position,
            z,
            quantity);
    }
}
