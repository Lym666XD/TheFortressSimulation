using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Construction;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Jobs;

internal sealed class ConstructionDiffEmitter : IConstructionDiffEmitter
{
    private readonly DiffLog? _diff;
    private readonly ItemsDiffLog _itemsDiff;
    private readonly string _systemId;
    private readonly int _priority;

    public ConstructionDiffEmitter(DiffLog? diff, ItemsDiffLog itemsDiff, string systemId, int priority)
    {
        _diff = diff;
        _itemsDiff = itemsDiff ?? throw new ArgumentNullException(nameof(itemsDiff));
        _systemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
        _priority = priority;
    }

    public bool CanEmitWorldDiffs => _diff != null;

    public void SetTerrain(Point cell, int z, TerrainKind kind)
    {
        if (_diff == null) return;
        if (!WorldCellTargetEncoding.TryEncode(cell, z, out var target)) return;

        ulong args = ConstructionSystem.PackSetTerrainArgs(kind, 0);
        _diff.AddOp(new DiffOp(DiffOpType.SetTerrain, target.ToDiffTarget(), _systemId, _priority, args));
    }

    public void RemoveItem(Guid itemGuid, Point cell, int z, int quantity)
    {
        if (itemGuid == Guid.Empty || quantity <= 0) return;
        if (!WorldCellTargetEncoding.TryEncode(cell, z, out var target)) return;
        _itemsDiff.AddRemoveItem(itemGuid, target, quantity, _priority, _systemId);
    }

    public void MoveItem(Guid itemId, Point dest, int z)
    {
        if (_diff == null || itemId == Guid.Empty) return;
        if (!WorldCellTargetEncoding.TryEncode(dest, z, out var target)) return;

        _diff.AddOp(new DiffOp(DiffOpType.MoveItem, target.ToDiffTarget(DiffTargetEncoding.SignedEntityId(itemId)), _systemId, _priority));
    }

    public void MoveCreature(Guid creatureId, Point3 dest)
    {
        if (_diff == null) return;
        if (!WorldCellTargetEncoding.TryEncode(dest.X, dest.Y, dest.Z, out var target)) return;

        _diff.AddOp(new DiffOp(DiffOpType.MoveCreature, target.ToDiffTarget(DiffTargetEncoding.SignedEntityId(creatureId)), _systemId, _priority));
    }
}
