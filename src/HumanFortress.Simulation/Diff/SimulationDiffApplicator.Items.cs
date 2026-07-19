using System;
using HumanFortress.Core.Simulation;
using SadRogue.Primitives;
using SimulationChunk = HumanFortress.Simulation.World.Chunk;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Diff;

internal static partial class SimulationDiffApplicator
{
    private static void ApplyMoveItem(SimulationWorld world, DiffOp op, ulong currentTick)
    {
        var (ck, lx, ly) = DecodeTarget(op.Target);
        int worldX = ck.ChunkX * SimulationChunk.SIZE_XY + lx;
        int worldY = ck.ChunkY * SimulationChunk.SIZE_XY + ly;
        int worldZ = ck.Z;

        var item = FindItemByTarget(world, op.Target);
        if (item == null)
            throw new InvalidOperationException("MoveItem target does not resolve to a live item.");

        var newPos = new Point(worldX, worldY);
        if (!world.Items.UpdateItemPosition(item.Guid, newPos, worldZ))
            throw new InvalidOperationException($"MoveItem rejected for {item.Guid}.");

        try
        {
            var merge = world.Items.MergeStacksAt(newPos, worldZ, currentTick);
            if (merge.Changed)
            {
                Emit(world, $"[DIFF][Items] MergeStacksAt ({worldX},{worldY},{worldZ}) transferred={merge.TransferredQuantity} removed={merge.RemovedInstanceCount}");
            }
        }
        catch (Exception ex)
        {
            EmitError(world, $"[DIFF][Items] MergeStacksAt exception: {ex.Message}", ex);
            throw;
        }
    }

    private static void ApplyMarkCarried(SimulationWorld world, DiffOp op)
    {
        var (ck, lx, ly) = DecodeTarget(op.Target);
        int worldX = ck.ChunkX * SimulationChunk.SIZE_XY + lx;
        int worldY = ck.ChunkY * SimulationChunk.SIZE_XY + ly;
        int worldZ = ck.Z;

        var item = FindItemByTarget(world, op.Target);
        if (item == null)
            throw new InvalidOperationException("MarkCarried target does not resolve to a live item.");

        var carrier = FindCreatureByEntityArgument(world, op);
        if (carrier == null)
            throw new InvalidOperationException("MarkCarried carrier does not resolve to a live creature.");

        var carryPos = new Point(worldX, worldY);
        if (item.Position != carryPos || item.Z != worldZ)
        {
            world.Items.UpdateItemPosition(item.Guid, carryPos, worldZ);
        }
        item.CarriedBy = carrier.Guid;
    }

    private static void ApplyUnmarkCarried(SimulationWorld world, DiffOp op, ulong currentTick)
    {
        var (ck, lx, ly) = DecodeTarget(op.Target);
        int worldX = ck.ChunkX * SimulationChunk.SIZE_XY + lx;
        int worldY = ck.ChunkY * SimulationChunk.SIZE_XY + ly;
        int worldZ = ck.Z;

        var item = FindItemByTarget(world, op.Target);
        if (item == null)
            throw new InvalidOperationException("UnmarkCarried target does not resolve to a live item.");

        var dropPos = new Point(worldX, worldY);
        if (item.Position != dropPos || item.Z != worldZ)
        {
            world.Items.UpdateItemPosition(item.Guid, dropPos, worldZ);
        }
        item.CarriedBy = null;

        try
        {
            var merge = world.Items.MergeStacksAt(item.Position, item.Z, currentTick);
            if (merge.Changed)
            {
                Emit(world, $"[DIFF][Items] MergeStacksAt (uncarry) ({item.Position.X},{item.Position.Y},{item.Z}) transferred={merge.TransferredQuantity} removed={merge.RemovedInstanceCount}");
            }
        }
        catch (Exception ex)
        {
            EmitError(world, $"[DIFF][Items] MergeStacksAt (uncarry) exception: {ex.Message}", ex);
            throw;
        }
    }
}
