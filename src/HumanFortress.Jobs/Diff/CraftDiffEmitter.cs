using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Craft;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Diff;

internal sealed class CraftDiffEmitter : ICraftDiffEmitter
{
    private readonly ItemsDiffLog _itemsDiff;
    private readonly WorldModel? _world;
    private readonly StockpileDiffLog? _stockpileDiffs;
    private readonly int _priority;
    private readonly string _systemId;

    internal CraftDiffEmitter(
        ItemsDiffLog itemsDiff,
        int priority,
        string systemId,
        WorldModel? world = null,
        StockpileDiffLog? stockpileDiffs = null)
    {
        _itemsDiff = itemsDiff ?? throw new ArgumentNullException(nameof(itemsDiff));
        _world = world;
        _stockpileDiffs = stockpileDiffs;
        _priority = priority;
        _systemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
    }

    internal void AddItem(Point cell, int z, string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0) return;
        if (!WorldCellTargetEncoding.TryEncode(cell, z, out var target)) return;
        _itemsDiff.Add(ItemsDiffOp.AddItem, target, itemId, quantity, _priority, _systemId);
    }

    internal void RemoveItem(Guid itemGuid, Point cell, int z, int quantity)
    {
        if (itemGuid == Guid.Empty || quantity <= 0) return;
        if (!WorldCellTargetEncoding.TryEncode(cell, z, out var target)) return;
        _itemsDiff.AddRemoveItem(itemGuid, target, quantity, _priority, _systemId);
        RecordStockpileStackRemoval(itemGuid, cell, z, quantity);
    }

    void ICraftDiffEmitter.AddItem(Point cell, int z, string itemId, int quantity) => AddItem(cell, z, itemId, quantity);

    void ICraftDiffEmitter.RemoveItem(Guid itemGuid, Point cell, int z, int quantity) => RemoveItem(itemGuid, cell, z, quantity);

    private void RecordStockpileStackRemoval(Guid itemGuid, Point cell, int z, int quantity)
    {
        if (_world == null || _stockpileDiffs == null)
            return;

        var item = _world.Items.GetInstance(itemGuid);
        if (item == null || quantity < item.StackCount)
            return;

        if (!StockpileWorldQueries.TryGetStockpileCell(_world, cell.X, cell.Y, z, out var location))
            return;

        _stockpileDiffs.AddRemoveItem(
            DiffTargetEncoding.EntityKey(itemGuid),
            location.ChunkKey,
            location.CellIndex,
            location.ZoneId,
            quantity: 1,
            priority: _priority,
            systemId: _systemId,
            StockpileItemProjection.FromItem(item, _world.Items.GetDefinition(item.DefinitionId)));
    }
}
