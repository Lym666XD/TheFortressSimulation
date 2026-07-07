using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal sealed class ItemSpawnCommandTarget : IItemSpawnCommandTarget
{
    private readonly World _world;
    private readonly ItemsDiffLog _itemsDiffLog;

    internal ItemSpawnCommandTarget(World world, ItemsDiffLog itemsDiffLog)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _itemsDiffLog = itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog));
    }

    bool IItemSpawnCommandTarget.AddItemSpawn(string itemId, Point worldPos, int z, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0) return false;
        if (worldPos.X < 0 || worldPos.Y < 0 || z < 0) return false;
        if (!_world.IsValidPosition(worldPos.X, worldPos.Y, z)) return false;
        if (!WorldCellTargetEncoding.TryEncode(worldPos, z, out var target)) return false;

        _itemsDiffLog.Add(ItemsDiffOp.AddItem, target, itemId, quantity, priority: 100, systemId: "Commands.SpawnItem");
        return true;
    }
}
