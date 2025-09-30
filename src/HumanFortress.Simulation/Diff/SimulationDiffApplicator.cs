using System;
using System.Linq;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Diff;

/// <summary>
/// Applies core DiffLog operations to the simulation world (v1.1 minimal).
/// Handles MoveCreature, MoveItem, MarkCarried, UnmarkCarried.
/// This runs after systems' Write phase per UPDATE_ORDER.
/// </summary>
public static class SimulationDiffApplicator
{
    public static void ApplyAll(World.World world, System.Collections.Generic.IReadOnlyList<DiffOp> ops)
    {
        if (ops.Count == 0) return;

        foreach (var op in ops)
        {
            try
            {
                switch (op.Op)
                {
                    case DiffOpType.MoveCreature:
                        ApplyMoveCreature(world, op);
                        break;
                    case DiffOpType.MoveItem:
                        ApplyMoveItem(world, op);
                        break;
                    case DiffOpType.MarkCarried:
                        ApplyMarkCarried(world, op);
                        break;
                    case DiffOpType.UnmarkCarried:
                        ApplyUnmarkCarried(world, op);
                        break;
                    default:
                        // Ignore other ops here (Stockpile uses its own applicator)
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimulationDiffApplicator] Failed to apply {op.Op}: {ex.Message}");
            }
        }
    }

    private static void ApplyMoveCreature(World.World world, DiffOp op)
    {
        var (ck, lx, ly) = DecodeTarget(op.Target);
        int worldX = ck.ChunkX * World.Chunk.SIZE_XY + lx;
        int worldY = ck.ChunkY * World.Chunk.SIZE_XY + ly;
        int worldZ = ck.Z;

        var creature = world.Creatures.GetAllInstances().FirstOrDefault(c => ToEntity(c.Guid) == (uint)op.Target.EntityId);
        if (creature == null) return;

        creature.Position = new SadRogue.Primitives.Point(worldX, worldY);
        creature.Z = worldZ;
    }

    private static void ApplyMoveItem(World.World world, DiffOp op)
    {
        var (ck, lx, ly) = DecodeTarget(op.Target);
        int worldX = ck.ChunkX * World.Chunk.SIZE_XY + lx;
        int worldY = ck.ChunkY * World.Chunk.SIZE_XY + ly;
        int worldZ = ck.Z;

        var item = world.Items.GetAllInstances().FirstOrDefault(i => ToEntity(i.Guid) == (uint)op.Target.EntityId);
        if (item == null) return;

        item.Position = new SadRogue.Primitives.Point(worldX, worldY);
        item.Z = worldZ;
    }

    private static void ApplyMarkCarried(World.World world, DiffOp op)
    {
        var item = world.Items.GetAllInstances().FirstOrDefault(i => ToEntity(i.Guid) == (uint)op.Target.EntityId);
        if (item == null) return;

        // Args low 32 bits carry carrier entity id (uint)
        uint carrierEid = (uint)(op.Args & 0xFFFFFFFFUL);
        var carrier = world.Creatures.GetAllInstances().FirstOrDefault(c => ToEntity(c.Guid) == carrierEid);

        item.IsCarried = true;
        item.CarriedBy = carrier?.Guid;
    }

    private static void ApplyUnmarkCarried(World.World world, DiffOp op)
    {
        var item = world.Items.GetAllInstances().FirstOrDefault(i => ToEntity(i.Guid) == (uint)op.Target.EntityId);
        if (item == null) return;

        item.IsCarried = false;
        item.CarriedBy = null;
        item.IsReserved = false;
        item.ReservedBy = null;
    }

    private static (World.ChunkKey ck, int lx, int ly) DecodeTarget(DiffTarget target)
    {
        // Decode ChunkKey packed as (z<<20)|(x<<10)|y
        int z = (target.ChunkId >> 20) & 0x3FF;
        int x = (target.ChunkId >> 10) & 0x3FF;
        int y = (target.ChunkId) & 0x3FF;
        int lx = target.LocalIndex % World.Chunk.SIZE_XY;
        int ly = target.LocalIndex / World.Chunk.SIZE_XY;
        return (new World.ChunkKey(x, y, z), lx, ly);
    }

    private static uint ToEntity(Guid g)
    {
        var bytes = g.ToByteArray();
        return BitConverter.ToUInt32(bytes, 0);
    }
}

