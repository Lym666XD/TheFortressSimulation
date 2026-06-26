using SadRogue.Primitives;

namespace HumanFortress.Runtime;

internal interface IItemSpawnCommandTarget
{
    bool AddItemSpawn(string itemId, Point worldPos, int z, int quantity);
}
