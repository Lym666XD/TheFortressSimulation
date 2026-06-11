using System;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Transport;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Jobs;

internal sealed class TransportDiffEmitter : ITransportMovementDiffEmitter, ITransportItemDiffEmitter
{
    private const ulong SplitStackGuidScope = 0x4841554C53504C54UL; // HAULSPLT

    private readonly DiffLog? _diff;
    private readonly ItemsDiffLog _itemsDiff;
    private readonly int _priority;
    private readonly string _systemId;

    public TransportDiffEmitter(DiffLog? diff, ItemsDiffLog itemsDiff, int priority, string systemId)
    {
        _diff = diff;
        _itemsDiff = itemsDiff ?? throw new ArgumentNullException(nameof(itemsDiff));
        _priority = priority;
        _systemId = systemId;
    }

    public void MoveCreature(uint entityId, Point3 position)
    {
        if (_diff == null) return;
        var target = DiffTargetEncoding.ForWorldCell(position.X, position.Y, position.Z, unchecked((int)entityId));
        _diff.AddOp(new DiffOp(DiffOpType.MoveCreature, target, _systemId, _priority));
    }

    public void MoveItem(Guid itemId, Point3 dest)
    {
        if (_diff == null) return;
        var target = DiffTargetEncoding.ForWorldCell(dest.X, dest.Y, dest.Z, DiffTargetEncoding.SignedEntityId(itemId));
        _diff.AddOp(new DiffOp(DiffOpType.MoveItem, target, _systemId, _priority));
    }

    public bool SplitStack(Guid sourceItemId, Guid newItemId, SadRogue.Primitives.Point sourcePosition, int sourceZ, int quantity)
    {
        if (!WorldCellTargetEncoding.TryEncode(sourcePosition, sourceZ, out var target)) return false;

        _itemsDiff.AddSplitStack(sourceItemId, newItemId, target, quantity, _priority, _systemId);
        return true;
    }

    public bool SplitStack(Guid sourceItemId, Guid newItemId, int sourceX, int sourceY, int sourceZ, int quantity)
    {
        return SplitStack(sourceItemId, newItemId, new SadRogue.Primitives.Point(sourceX, sourceY), sourceZ, quantity);
    }

    public Guid GenerateSplitStackItemGuid(Guid sourceItemId, Guid creatureId, ulong tick, int quantity)
    {
        ulong salt = tick ^ ((ulong)(uint)quantity << 32) ^ DiffTargetEncoding.EntityId(creatureId);
        return DeterministicGuidGenerator.GenerateFromGuid(SplitStackGuidScope, sourceItemId, salt);
    }

    public void MarkCarried(Guid itemId, Guid carrierId, Point3 at)
    {
        if (_diff == null) return;
        uint carrierEntityId = DiffTargetEncoding.EntityId(carrierId);
        var target = DiffTargetEncoding.ForWorldCell(at.X, at.Y, at.Z, DiffTargetEncoding.SignedEntityId(itemId));
        _diff.AddOp(new DiffOp(DiffOpType.MarkCarried, target, _systemId, _priority, carrierEntityId));
    }

    public void UnmarkCarried(Guid itemId, Point3 at)
    {
        if (_diff == null) return;
        var target = DiffTargetEncoding.ForWorldCell(at.X, at.Y, at.Z, DiffTargetEncoding.SignedEntityId(itemId));
        _diff.AddOp(new DiffOp(DiffOpType.UnmarkCarried, target, _systemId, _priority));
    }

}
