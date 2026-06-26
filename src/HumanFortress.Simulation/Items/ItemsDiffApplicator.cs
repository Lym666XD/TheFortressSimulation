using System;
using System.Collections.Generic;
using HumanFortress.Simulation.Diagnostics;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

/// <summary>
/// Applies ItemsDiff operations during the post-tick write boundary.
/// </summary>
internal static class ItemsDiffApplicator
{
    public static Action<string>? LogCallback { get; set; }

    public static void ApplyAll(World.World world, IReadOnlyList<ItemsDiff> diffs, ulong tick)
    {
        ApplyPreSimulation(world, diffs);
        ApplyAdditions(world, diffs, tick);
    }

    public static void ApplyPreSimulation(World.World world, IReadOnlyList<ItemsDiff> diffs)
    {
        ApplyRemovals(world, diffs);
        ApplySplits(world, diffs);
    }

    public static void ApplyRemovals(World.World world, IReadOnlyList<ItemsDiff> diffs)
    {
        if (diffs.Count == 0) return;
        foreach (var d in diffs)
        {
            if (d.Op != ItemsDiffOp.RemoveItem) continue;
            try
            {
                ApplyRemoveItem(world, d);
            }
            catch (Exception ex)
            {
                Emit($"[ItemsDiffApplicator] Failed to apply {d.Op} at {d.Chunk}: {ex.Message}");
            }
        }
    }

    public static void ApplyAdditions(World.World world, IReadOnlyList<ItemsDiff> diffs, ulong tick)
    {
        if (diffs.Count == 0) return;
        foreach (var d in diffs)
        {
            if (d.Op != ItemsDiffOp.AddItem) continue;
            try
            {
                ApplyAddItem(world, d, tick);
            }
            catch (Exception ex)
            {
                Emit($"[ItemsDiffApplicator] Failed to apply {d.Op} at {d.Chunk}: {ex.Message}");
            }
        }
    }

    public static void ApplySplits(World.World world, IReadOnlyList<ItemsDiff> diffs)
    {
        if (diffs.Count == 0) return;
        foreach (var d in diffs)
        {
            if (d.Op != ItemsDiffOp.SplitStack) continue;
            try
            {
                ApplySplitStack(world, d);
            }
            catch (Exception ex)
            {
                Emit($"[ItemsDiffApplicator] Failed to apply {d.Op} at {d.Chunk}: {ex.Message}");
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

    private static void ApplyRemoveItem(World.World world, ItemsDiff d)
    {
        if (d.ItemGuid == Guid.Empty || d.Quantity <= 0) return;

        var item = world.Items.GetInstance(d.ItemGuid);
        if (item == null) return;

        int removed = Math.Min(item.StackCount, d.Quantity);
        item.StackCount -= removed;

        if (item.StackCount <= 0)
        {
            world.Items.RemoveInstance(item.Guid);
        }
    }

    private static void ApplySplitStack(World.World world, ItemsDiff d)
    {
        if (d.ItemGuid == Guid.Empty || d.NewItemGuid == Guid.Empty || d.Quantity <= 0) return;
        world.Items.SplitStackWithGuid(d.ItemGuid, d.Quantity, d.NewItemGuid);
    }

    private static void Emit(string message)
    {
        SimulationDiagnostics.Error(LogCallback, "Simulation.ItemsDiff", message);
    }
}
