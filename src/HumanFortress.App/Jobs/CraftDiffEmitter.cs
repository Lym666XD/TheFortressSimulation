using System;
using HumanFortress.Jobs.Craft;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Jobs;

internal sealed class CraftDiffEmitter : ICraftDiffEmitter
{
    private readonly ItemsDiffLog _itemsDiff;
    private readonly int _priority;
    private readonly string _systemId;

    public CraftDiffEmitter(ItemsDiffLog itemsDiff, int priority, string systemId)
    {
        _itemsDiff = itemsDiff ?? throw new ArgumentNullException(nameof(itemsDiff));
        _priority = priority;
        _systemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
    }

    public void AddItem(Point cell, int z, string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0) return;
        if (!WorldCellTargetEncoding.TryEncode(cell, z, out var target)) return;
        _itemsDiff.Add(ItemsDiffOp.AddItem, target, itemId, quantity, _priority, _systemId);
    }

    public void RemoveItem(Guid itemGuid, Point cell, int z, int quantity)
    {
        if (itemGuid == Guid.Empty || quantity <= 0) return;
        if (!WorldCellTargetEncoding.TryEncode(cell, z, out var target)) return;
        _itemsDiff.AddRemoveItem(itemGuid, target, quantity, _priority, _systemId);
    }
}
