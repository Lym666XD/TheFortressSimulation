using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime.Diff;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Zones;

namespace HumanFortress.Runtime.Host;

internal enum TickMutationCommitStage
{
    Preparation = 0,
    ItemsPreSimulation = 1,
    CoreSimulation = 2,
    Creatures = 3,
    ItemsAdditions = 4,
    Orders = 5,
    Workshops = 6,
    Zones = 7,
    Stockpiles = 8,
    Professions = 9
}

internal sealed class TickMutationCommitFaultException : Exception
{
    internal TickMutationCommitFaultException(
        ulong tick,
        TickMutationCommitStage stage,
        bool rollbackSucceeded,
        Exception cause,
        Exception? rollbackCause = null)
        : base(
            $"Tick {tick} mutation commit failed at {stage}; rollbackSucceeded={rollbackSucceeded}.",
            rollbackCause == null ? cause : new AggregateException(cause, rollbackCause))
    {
        Tick = tick;
        Stage = stage;
        RollbackSucceeded = rollbackSucceeded;
        CommitCause = cause;
        RollbackCause = rollbackCause;
    }

    internal ulong Tick { get; }
    internal TickMutationCommitStage Stage { get; }
    internal bool RollbackSucceeded { get; }
    internal Exception CommitCause { get; }
    internal Exception? RollbackCause { get; }
}

internal sealed class TickMutationCommitTransaction
{
    private readonly World _world;
    private readonly DiffLog _coreLog;
    private readonly RuntimeMutationDiffLogs _typedLogs;
    private readonly IConstructionCatalog _constructions;
    private readonly IRuntimeGeologyCatalog? _geology;
    private readonly Action<TickMutationCommitStage>? _afterStage;

    internal TickMutationCommitTransaction(
        World world,
        DiffLog coreLog,
        RuntimeMutationDiffLogs typedLogs,
        IConstructionCatalog constructions,
        IRuntimeGeologyCatalog? geology,
        Action<TickMutationCommitStage>? afterStage = null)
    {
        _world = world;
        _coreLog = coreLog;
        _typedLogs = typedLogs;
        _constructions = constructions;
        _geology = geology;
        _afterStage = afterStage;
    }

    internal void Commit(ulong tick)
    {
        // No authority writes are allowed before every family has merged and
        // passed structural preflight.
        PreparedPlan plan;
        try
        {
            plan = Prepare();
        }
        catch (Exception cause)
        {
            throw new TickMutationCommitFaultException(
                tick,
                TickMutationCommitStage.Preparation,
                rollbackSucceeded: true,
                cause: cause);
        }
        lock (_world.MutationCommitLock)
        {
            var worldMemento = _world.CaptureMutationMemento();
            var stage = TickMutationCommitStage.ItemsPreSimulation;
            try
            {
                ItemsDiffApplicator.ApplyPreSimulation(_world, plan.Items, tick);
                Probe(stage);

                stage = TickMutationCommitStage.CoreSimulation;
                SimulationDiffApplicator.ApplyAll(_world, plan.Core, _geology, tick);
                Probe(stage);

                stage = TickMutationCommitStage.Creatures;
                CreaturesDiffApplicator.ApplyAll(_world, plan.Creatures, tick);
                Probe(stage);

                stage = TickMutationCommitStage.ItemsAdditions;
                ItemsDiffApplicator.ApplyAdditions(_world, plan.Items, tick);
                Probe(stage);

                stage = TickMutationCommitStage.Orders;
                OrderDiffApplicator.ApplyAll(_world, plan.Orders);
                Probe(stage);

                stage = TickMutationCommitStage.Workshops;
                WorkshopDiffApplicator.ApplyAll(_world, plan.Workshops, _constructions);
                Probe(stage);

                stage = TickMutationCommitStage.Zones;
                ZoneDiffApplicator.ApplyAll(_world, plan.Zones);
                Probe(stage);

                stage = TickMutationCommitStage.Stockpiles;
                StockpileDiffApplicator.ApplyAll(_world, plan.Stockpiles);
                Probe(stage);

                stage = TickMutationCommitStage.Professions;
                _typedLogs.Professions.Apply(plan.Professions);
                Probe(stage);

                _coreLog.Clear();
                _typedLogs.Clear();
            }
            catch (Exception commitCause)
            {
                Exception? rollbackCause = null;
                try
                {
                    ProfessionAssignmentDiffLog.Rollback(plan.Professions);
                    _world.RestoreMutationMemento(worldMemento);
                }
                catch (Exception ex)
                {
                    rollbackCause = ex;
                }

                throw new TickMutationCommitFaultException(
                    tick,
                    stage,
                    rollbackCause == null,
                    commitCause,
                    rollbackCause);
            }
        }
    }

    private PreparedPlan Prepare()
    {
        var plan = new PreparedPlan(
            _typedLogs.Items.MergeAndSort(),
            _coreLog.MergeAndSort(),
            _typedLogs.Creatures.MergeAndSort(),
            _typedLogs.Orders.MergeAndSort(),
            _typedLogs.Workshops.MergeAndSort(),
            _typedLogs.Zones.MergeAndSort(),
            _typedLogs.Stockpiles.MergeAndSort(),
            _typedLogs.Professions.Prepare());
        Validate(plan);
        return plan;
    }

    private void Probe(TickMutationCommitStage stage) => _afterStage?.Invoke(stage);

    private void Validate(PreparedPlan plan)
    {
        foreach (var diff in plan.Items)
        {
            Require(Enum.IsDefined(diff.Op), "Items diff has an unsupported operation.");
            Require(!string.IsNullOrWhiteSpace(diff.SystemId), "Items diff has a blank system id.");
            Require(diff.LocalIndex is >= 0 and < Chunk.CELLS_PER_LAYER, "Items diff has an invalid local index.");
            Require(diff.Quantity > 0, "Items diff has a non-positive quantity.");
            if (diff.Op == ItemsDiffOp.AddItem)
                Require(!string.IsNullOrWhiteSpace(diff.ItemId), "Add-item diff has a blank item id.");
            else
                Require(diff.ItemGuid != Guid.Empty, "Item mutation diff has an empty item guid.");
            if (diff.Op == ItemsDiffOp.SplitStack)
                Require(diff.NewItemGuid != Guid.Empty, "Split-item diff has an empty destination guid.");
        }

        foreach (var diff in plan.Core)
        {
            Require(
                diff.Op is DiffOpType.SetTerrain or DiffOpType.MoveCreature or DiffOpType.MoveItem
                    or DiffOpType.MarkCarried or DiffOpType.UnmarkCarried,
                $"Core diff operation {diff.Op} has no transactional applicator.");
            Require(!string.IsNullOrWhiteSpace(diff.SystemId), "Core diff has a blank system id.");
            Require(diff.Target.LocalIndex is >= 0 and < Chunk.CELLS_PER_LAYER, "Core diff has an invalid local index.");
            var (chunkX, chunkY, z) = DiffTargetEncoding.DecodeChunkId(diff.Target.ChunkId);
            var (localX, localY) = DiffTargetEncoding.DecodeLocalIndex(diff.Target.LocalIndex);
            Require(_world.IsValidPosition(
                chunkX * Chunk.SIZE_XY + localX,
                chunkY * Chunk.SIZE_XY + localY,
                z), "Core diff targets a position outside the world.");
            if (diff.Op == DiffOpType.SetTerrain)
                Require(Enum.IsDefined((HumanFortress.Simulation.Tiles.TerrainKind)(byte)diff.Args), "Terrain diff has an invalid kind.");
        }

        foreach (var diff in plan.Creatures)
        {
            Require(Enum.IsDefined(diff.Op), "Creature diff has an unsupported operation.");
            Require(!string.IsNullOrWhiteSpace(diff.CreatureId), "Creature diff has a blank definition id.");
            Require(!string.IsNullOrWhiteSpace(diff.FactionId), "Creature diff has a blank faction id.");
            Require(_world.IsValidPosition(diff.WorldPos.X, diff.WorldPos.Y, diff.Z), "Creature diff targets a position outside the world.");
        }

        foreach (var diff in plan.Orders)
        {
            Require(Enum.IsDefined(diff.Op), "Order diff has an unsupported operation.");
            Require(!string.IsNullOrWhiteSpace(diff.SystemId), "Order diff has a blank system id.");
            Require(diff.WorldRect.Width > 0 && diff.WorldRect.Height > 0, "Order diff has an empty rectangle.");
        }

        foreach (var diff in plan.Workshops)
        {
            Require(Enum.IsDefined(diff.Op), "Workshop diff has an unsupported operation.");
            Require(diff.WorkshopGuid != Guid.Empty, "Workshop diff has an empty workshop guid.");
            Require(!string.IsNullOrWhiteSpace(diff.SystemId), "Workshop diff has a blank system id.");
        }

        foreach (var diff in plan.Zones)
        {
            Require(Enum.IsDefined(diff.Op), "Zone diff has an unsupported operation.");
            Require(!string.IsNullOrWhiteSpace(diff.SystemId), "Zone diff has a blank system id.");
            if (diff.Op != ZoneDiffOp.DeleteZone)
                Require(diff.WorldRect.Width > 0 && diff.WorldRect.Height > 0, "Zone diff has an empty rectangle.");
        }

        foreach (var diff in plan.Stockpiles)
        {
            Require(Enum.IsDefined(diff.Op), "Stockpile diff has an unsupported operation.");
            Require(!string.IsNullOrWhiteSpace(diff.SystemId), "Stockpile diff has a blank system id.");
            if (diff.CellIndex >= 0)
                Require(diff.CellIndex < Chunk.CELLS_PER_LAYER, "Stockpile diff has an invalid local index.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly record struct PreparedPlan(
        IReadOnlyList<ItemsDiff> Items,
        IReadOnlyList<DiffOp> Core,
        IReadOnlyList<CreaturesDiff> Creatures,
        IReadOnlyList<OrderDiff> Orders,
        IReadOnlyList<WorkshopDiff> Workshops,
        IReadOnlyList<ZoneDiff> Zones,
        IReadOnlyList<StockpileDiff> Stockpiles,
        ProfessionAssignmentDiffLog.PreparedProfessionAssignmentDiffs Professions);
}
