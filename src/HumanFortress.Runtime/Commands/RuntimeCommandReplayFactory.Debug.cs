using System.IO;
using HumanFortress.Core.Commands;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal sealed partial class RuntimeCommandReplayFactory
{
    private static ICommand DecodeSpawnItem(ulong tick, BinaryReader reader)
    {
        var itemId = reader.ReadString();
        var position = new Point(reader.ReadInt32(), reader.ReadInt32());
        var z = reader.ReadInt32();
        var quantity = reader.ReadInt32();
        return new SpawnItemCommand(tick, itemId, position, z, quantity);
    }

    private static ICommand DecodeSpawnCreature(ulong tick, BinaryReader reader)
    {
        var creatureId = reader.ReadString();
        var position = new Point(reader.ReadInt32(), reader.ReadInt32());
        var z = reader.ReadInt32();
        var factionId = reader.ReadString();
        return new SpawnCreatureCommand(tick, creatureId, position, z, factionId);
    }
}
