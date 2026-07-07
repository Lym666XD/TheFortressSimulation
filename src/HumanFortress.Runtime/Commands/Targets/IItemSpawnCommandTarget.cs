using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal interface IItemSpawnCommandTarget
{
    bool AddItemSpawn(string itemId, Point worldPos, int z, int quantity);
}
