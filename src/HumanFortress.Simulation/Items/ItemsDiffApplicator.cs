using System;
using System.Collections.Generic;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

/// <summary>
/// Applies ItemsDiff operations (AddItem/RemoveItem) during Write phase.
/// </summary>
public static class ItemsDiffApplicator
{
    public static void ApplyAll(World.World world, IReadOnlyList<ItemsDiff> diffs, ulong tick)
    {
        if (diffs.Count == 0) return;
        foreach (var d in diffs)
        {
            try
            {
                switch (d.Op)
                {
                    case ItemsDiffOp.AddItem:
                        ApplyAddItem(world, d, tick);
                        break;
                    case ItemsDiffOp.RemoveItem:
                        // TODO: Implement removal by handle/position; not needed in current step
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ItemsDiffApplicator] Failed to apply {d.Op} at {d.Chunk}: {ex.Message}");
            }
        }
    }

    private static void ApplyAddItem(World.World world, ItemsDiff d, ulong tick)
    {
        int lx = d.LocalIndex % World.Chunk.SIZE_XY;
        int ly = d.LocalIndex / World.Chunk.SIZE_XY;
        int wx = d.Chunk.ChunkX * World.Chunk.SIZE_XY + lx;
        int wy = d.Chunk.ChunkY * World.Chunk.SIZE_XY + ly;
        world.Items.SpawnItem(d.ItemId, new Point(wx, wy), d.Chunk.Z, d.Quantity, tick);
    }
}

