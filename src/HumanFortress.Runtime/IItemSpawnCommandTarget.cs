using SadRogue.Primitives;

namespace HumanFortress.Runtime;

public interface IItemSpawnCommandTarget
{
    bool AddItemSpawn(string itemId, Point worldPos, int z, int quantity);
}
