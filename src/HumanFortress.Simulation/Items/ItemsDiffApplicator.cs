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
    internal static void ApplyAll(World.World world, IReadOnlyList<ItemsDiff> diffs, ulong tick)
    {
        ApplyPreSimulation(world, diffs, tick);
        ApplyAdditions(world, diffs, tick);
    }

    internal static void ApplyPreSimulation(
        World.World world,
        IReadOnlyList<ItemsDiff> diffs,
        ulong currentTick = 0)
    {
        ApplyRemovals(world, diffs, currentTick);
        ApplySplits(world, diffs, currentTick);
    }

    internal static void ApplyRemovals(
        World.World world,
        IReadOnlyList<ItemsDiff> diffs,
        ulong currentTick = 0)
    {
        if (diffs.Count == 0) return;
        foreach (var d in diffs)
        {
            if (d.Op != ItemsDiffOp.RemoveItem) continue;
            try
            {
                ApplyRemoveItem(world, d, currentTick);
            }
            catch (Exception ex)
            {
                Emit(world, $"[ItemsDiffApplicator] Failed to apply {d.Op} at {d.Chunk}: {ex.Message}");
                throw;
            }
        }
    }

    internal static void ApplyAdditions(World.World world, IReadOnlyList<ItemsDiff> diffs, ulong tick)
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
                Emit(world, $"[ItemsDiffApplicator] Failed to apply {d.Op} at {d.Chunk}: {ex.Message}");
                throw;
            }
        }
    }

    internal static void ApplySplits(
        World.World world,
        IReadOnlyList<ItemsDiff> diffs,
        ulong currentTick = 0)
    {
        if (diffs.Count == 0) return;
        foreach (var d in diffs)
        {
            if (d.Op != ItemsDiffOp.SplitStack) continue;
            try
            {
                ApplySplitStack(world, d, currentTick);
            }
            catch (Exception ex)
            {
                Emit(world, $"[ItemsDiffApplicator] Failed to apply {d.Op} at {d.Chunk}: {ex.Message}");
                throw;
            }
        }
    }

    private static void ApplyAddItem(World.World world, ItemsDiff d, ulong tick)
    {
        int lx = d.LocalIndex % World.Chunk.SIZE_XY;
        int ly = d.LocalIndex / World.Chunk.SIZE_XY;
        int wx = d.Chunk.ChunkX * World.Chunk.SIZE_XY + lx;
        int wy = d.Chunk.ChunkY * World.Chunk.SIZE_XY + ly;
        var result = world.Items.SpawnItems(d.ItemId, new Point(wx, wy), d.Chunk.Z, d.Quantity, tick);
        if (!result.Success)
        {
            Emit(world, $"[ItemsDiffApplicator] AddItem rejected for '{d.ItemId}': {result.Reason}");
            throw new InvalidOperationException(result.Reason);
        }
    }

    private static void ApplyRemoveItem(World.World world, ItemsDiff d, ulong currentTick)
    {
        if (d.ItemGuid == Guid.Empty || d.Quantity <= 0) return;

        var result = world.Items.RemoveQuantity(d.ItemGuid, d.Quantity, currentTick);
        if (result.Status == ItemMutationStatus.Rejected)
        {
            Emit(world, $"[ItemsDiffApplicator] RemoveItem rejected for {d.ItemGuid}: {result.Reason}");
            throw new InvalidOperationException(result.Reason);
        }
    }

    private static void ApplySplitStack(World.World world, ItemsDiff d, ulong currentTick)
    {
        if (d.ItemGuid == Guid.Empty || d.NewItemGuid == Guid.Empty || d.Quantity <= 0) return;
        var result = d.SourceReservation.IsValid || d.StagedReservation.IsValid
            ? world.Items.SplitReservedStackWithGuid(
                d.ItemGuid,
                d.Quantity,
                d.NewItemGuid,
                d.SourceReservation,
                d.StagedReservation,
                currentTick)
            : world.Items.SplitStackWithGuid(
                d.ItemGuid,
                d.Quantity,
                d.NewItemGuid,
                currentTick);
        if (!result.Success)
        {
            Emit(world, $"[ItemsDiffApplicator] SplitStack rejected for {d.ItemGuid}: {result.Reason}");
            throw new InvalidOperationException(result.Reason);
        }
    }

    private static void Emit(World.World world, string message)
    {
        SimulationDiagnostics.Error(world.Diagnostics, "Simulation.ItemsDiff", message);
    }
}
