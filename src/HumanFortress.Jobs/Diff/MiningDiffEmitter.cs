using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Mining;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Diff;

internal sealed class MiningDiffEmitter : IMiningDiffEmitter
{
    private readonly DiffLog? _diff;
    private readonly ItemsDiffLog? _itemsDiff;
    private readonly string _systemId;
    private readonly int _priority;

    internal MiningDiffEmitter(DiffLog? diff, ItemsDiffLog? itemsDiff, string systemId, int priority)
    {
        _diff = diff;
        _itemsDiff = itemsDiff;
        _systemId = systemId;
        _priority = priority;
    }

    internal void SetTerrainOpen(Point cell, int z)
    {
        if (_diff == null) return;
        var target = DiffTargetEncoding.ForWorldCell(cell.X, cell.Y, z);
        ulong args = (ulong)TerrainKind.OpenWithFloor;
        _diff.AddOp(new DiffOp(DiffOpType.SetTerrain, target, _systemId, _priority, args, JobDiffSystemOrder.Mining));
    }

    internal void SetTerrainKind(Point cell, int z, TerrainKind kind)
    {
        if (_diff == null) return;
        var target = DiffTargetEncoding.ForWorldCell(cell.X, cell.Y, z);
        ulong args = (ulong)kind;
        _diff.AddOp(new DiffOp(DiffOpType.SetTerrain, target, _systemId, _priority, args, JobDiffSystemOrder.Mining));
    }

    internal void SetTerrain(Point cell, int z, TerrainKind kind, ushort overrideGeology)
    {
        if (_diff == null) return;
        var target = DiffTargetEncoding.ForWorldCell(cell.X, cell.Y, z);
        ulong args = (ulong)kind | ((ulong)overrideGeology << 8);
        _diff.AddOp(new DiffOp(DiffOpType.SetTerrain, target, _systemId, _priority, args, JobDiffSystemOrder.Mining));
    }

    internal void AddItem(Point cell, int z, string itemId, int quantity)
    {
        if (_itemsDiff == null) return;
        if (!WorldCellTargetEncoding.TryEncode(cell, z, out var target)) return;
        _itemsDiff.Add(ItemsDiffOp.AddItem, target, itemId, quantity, _priority, _systemId);
    }

    internal void MoveCreature(Guid creatureId, Point3 position)
    {
        if (_diff == null) return;
        var target = DiffTargetEncoding.ForWorldCell(
            position.X,
            position.Y,
            position.Z,
            creatureId);
        _diff.AddOp(new DiffOp(DiffOpType.MoveCreature, target, _systemId, _priority, systemOrder: JobDiffSystemOrder.Mining));
    }

    void IMiningDiffEmitter.MoveCreature(Guid creatureId, Point3 position) => MoveCreature(creatureId, position);

    void IMiningDiffEmitter.SetTerrain(Point cell, int z, TerrainKind kind, ushort overrideGeology) =>
        SetTerrain(cell, z, kind, overrideGeology);

    void IMiningDiffEmitter.AddItem(Point cell, int z, string itemId, int quantity) => AddItem(cell, z, itemId, quantity);
}
