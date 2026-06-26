using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Construction;
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

    internal ConstructionDiffEmitter(DiffLog? diff, ItemsDiffLog itemsDiff, string systemId, int priority)
    {
        _diff = diff;
        _itemsDiff = itemsDiff ?? throw new ArgumentNullException(nameof(itemsDiff));
        _systemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
        _priority = priority;
    }

    internal bool CanEmitWorldDiffs => _diff != null;

    internal void SetTerrain(Point cell, int z, TerrainKind kind)
    {
        if (_diff == null) return;
        if (!WorldCellTargetEncoding.TryEncode(cell, z, out var target)) return;

        ulong args = ConstructionSystem.PackSetTerrainArgs(kind, 0);
        _diff.AddOp(new DiffOp(DiffOpType.SetTerrain, target.ToDiffTarget(), _systemId, _priority, args));
    }

    internal void RemoveItem(Guid itemGuid, Point cell, int z, int quantity)
    {
        if (itemGuid == Guid.Empty || quantity <= 0) return;
        if (!WorldCellTargetEncoding.TryEncode(cell, z, out var target)) return;
        _itemsDiff.AddRemoveItem(itemGuid, target, quantity, _priority, _systemId);
    }

    internal void MoveItem(Guid itemId, Point dest, int z)
    {
        if (_diff == null || itemId == Guid.Empty) return;
        if (!WorldCellTargetEncoding.TryEncode(dest, z, out var target)) return;

        _diff.AddOp(new DiffOp(DiffOpType.MoveItem, target.ToDiffTarget(DiffTargetEncoding.SignedEntityId(itemId)), _systemId, _priority));
    }

    internal void MoveCreature(Guid creatureId, Point3 dest)
    {
        if (_diff == null) return;
        if (!WorldCellTargetEncoding.TryEncode(dest.X, dest.Y, dest.Z, out var target)) return;

        _diff.AddOp(new DiffOp(DiffOpType.MoveCreature, target.ToDiffTarget(DiffTargetEncoding.SignedEntityId(creatureId)), _systemId, _priority));
    }

    bool IConstructionDiffEmitter.CanEmitWorldDiffs => CanEmitWorldDiffs;

    void IConstructionDiffEmitter.SetTerrain(Point cell, int z, TerrainKind kind) => SetTerrain(cell, z, kind);

    void IConstructionDiffEmitter.RemoveItem(Guid itemGuid, Point cell, int z, int quantity) => RemoveItem(itemGuid, cell, z, quantity);

    void IConstructionDiffEmitter.MoveItem(Guid itemId, Point dest, int z) => MoveItem(itemId, dest, z);

    void IConstructionDiffEmitter.MoveCreature(Guid creatureId, Point3 dest) => MoveCreature(creatureId, dest);
}
