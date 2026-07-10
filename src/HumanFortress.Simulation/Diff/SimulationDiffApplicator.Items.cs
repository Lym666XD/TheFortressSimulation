using System;
using HumanFortress.Core.Simulation;
using SadRogue.Primitives;
using SimulationChunk = HumanFortress.Simulation.World.Chunk;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Diff;

internal static partial class SimulationDiffApplicator
{
    private static void ApplyMoveItem(SimulationWorld world, DiffOp op)
    {
        var (ck, lx, ly) = DecodeTarget(op.Target);
        int worldX = ck.ChunkX * SimulationChunk.SIZE_XY + lx;
        int worldY = ck.ChunkY * SimulationChunk.SIZE_XY + ly;
        int worldZ = ck.Z;

        var item = FindItemByTarget(world, op.Target);
        if (item == null) return;

        var oldPos = item.Position;
        var oldZ = item.Z;
        var newPos = new Point(worldX, worldY);
        world.Items.UpdateItemPosition(item.Guid, oldPos, oldZ, newPos, worldZ);

        try
        {
            int removed = world.Items.MergeStacksAt(newPos, worldZ);
            if (removed > 0)
            {
                Emit($"[DIFF][Items] MergeStacksAt ({worldX},{worldY},{worldZ}) removed={removed}");
            }
        }
        catch (Exception ex)
        {
            EmitError($"[DIFF][Items] MergeStacksAt exception: {ex.Message}", ex);
        }
    }

    private static void ApplyMarkCarried(SimulationWorld world, DiffOp op)
    {
        var (ck, lx, ly) = DecodeTarget(op.Target);
        int worldX = ck.ChunkX * SimulationChunk.SIZE_XY + lx;
        int worldY = ck.ChunkY * SimulationChunk.SIZE_XY + ly;
        int worldZ = ck.Z;

        var item = FindItemByTarget(world, op.Target);
        if (item == null) return;

        var carrier = FindCreatureByEntityArgument(world, op);

        var oldPos = item.Position;
        int oldZ = item.Z;
        var carryPos = new Point(worldX, worldY);
        if (oldPos != carryPos || oldZ != worldZ)
        {
            world.Items.UpdateItemPosition(item.Guid, oldPos, oldZ, carryPos, worldZ);
        }
        item.CarriedBy = carrier?.Guid ?? Guid.Empty;
    }

    private static void ApplyUnmarkCarried(SimulationWorld world, DiffOp op)
    {
        var (ck, lx, ly) = DecodeTarget(op.Target);
        int worldX = ck.ChunkX * SimulationChunk.SIZE_XY + lx;
        int worldY = ck.ChunkY * SimulationChunk.SIZE_XY + ly;
        int worldZ = ck.Z;

        var item = FindItemByTarget(world, op.Target);
        if (item == null) return;

        var oldPos = item.Position;
        int oldZ = item.Z;
        var dropPos = new Point(worldX, worldY);
        if (oldPos != dropPos || oldZ != worldZ)
        {
            world.Items.UpdateItemPosition(item.Guid, oldPos, oldZ, dropPos, worldZ);
        }
        item.CarriedBy = null;

        try
        {
            int removed = world.Items.MergeStacksAt(item.Position, item.Z);
            if (removed > 0)
            {
                Emit($"[DIFF][Items] MergeStacksAt (uncarry) ({item.Position.X},{item.Position.Y},{item.Z}) removed={removed}");
            }
        }
        catch (Exception ex)
        {
            EmitError($"[DIFF][Items] MergeStacksAt (uncarry) exception: {ex.Message}", ex);
        }
    }
}
