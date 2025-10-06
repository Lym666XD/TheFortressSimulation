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
    /// <summary>
    /// Optional logging callback (set by App layer to write to fortress_debug.log)
    /// </summary>
    public static Action<string>? LogCallback { get; set; }

    public static void ApplyAll(World.World world, System.Collections.Generic.IReadOnlyList<DiffOp> ops)
    {
        if (ops.Count == 0) return;

        foreach (var op in ops)
        {
            try
            {
                switch (op.Op)
                {
                    case DiffOpType.SetTerrain:
                        ApplySetTerrain(world, op);
                        break;
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

    private static void ApplySetTerrain(World.World world, DiffOp op)
    {
        var (ck, lx, ly) = DecodeTarget(op.Target);
        var chunk = world.GetChunk(ck);
        if (chunk == null) return;

        // Get current tile and set new kind from Args
        var tile = chunk.GetTile(lx, ly);
        var kindVal = (byte)(op.Args & 0xFF);
        var newKind = (HumanFortress.Simulation.Tiles.TerrainKind)kindVal;
        // Optional geology override in Args[8..23] (16 bits). 0 = no override.
        ushort overrideGeo = (ushort)((op.Args >> 8) & 0xFFFF);

        // Base bits: update kind
        var bits = HumanFortress.Simulation.Tiles.TerrainBitOps.SetKind(tile.TerrainBits, newKind);
        // If this diff comes from construction systems, mark as constructed and set Modifiable per policy.
        if (op.SystemId.StartsWith("Orders.Construction", StringComparison.Ordinal) ||
            op.SystemId.StartsWith("Jobs.Construction", StringComparison.Ordinal))
        {
            bits = HumanFortress.Simulation.Tiles.TerrainBitOps.SetNatural(bits, false);
            bool modifiable = ck.Z > 0; // per policy: bottommost Z is non-modifiable
            bits = HumanFortress.Simulation.Tiles.TerrainBitOps.SetModifiable(bits, modifiable);
        }

        // Normalize geology handle to a variant that matches newKind (e.g., wall_rock_x -> floor_rock_x)
        // If a matching geology entry with same material and desired kind exists, switch GeoMatId accordingly.
        ushort newGeoHandle = tile.GeoMatId;
        try
        {
            if (overrideGeo != 0)
            {
                newGeoHandle = overrideGeo;
            }
            else
            {
                // Derive new geology from current material across all kinds
                var reg = HumanFortress.Core.Content.ContentRegistry.Instance;
                var geo = reg.GetGeologyByHandle(tile.GeoMatId);
                if (geo != null && reg.TryGetGeologyHandleByMaterialAndKind(geo.Material, newKind.ToString(), out var handle))
                    newGeoHandle = handle;
            }
        }
        catch { /* fallback to existing GeoMatId on any error */ }

        // Clear surface bits if this became open space (channel cut), since topsoil shouldn't remain
        byte newSurface = tile.SurfaceBits;
        if (newKind == HumanFortress.Simulation.Tiles.TerrainKind.OpenNoFloor)
            newSurface = 0;

        var newTile = new HumanFortress.Simulation.Tiles.TileBase(
            newGeoHandle,
            bits,
            newSurface,
            tile.FluidKind,
            tile.FluidDepth,
            tile.MetaBits,
            tile.TrafficCost);

        chunk.SetTile(lx, ly, newTile, 0);

        // Log terrain change for debugging
        int worldX = ck.ChunkX * World.Chunk.SIZE_XY + lx;
        int worldY = ck.ChunkY * World.Chunk.SIZE_XY + ly;
        LogCallback?.Invoke($"[DIFF] ApplySetTerrain at ({worldX},{worldY},{ck.Z}): {tile.Kind} → {newKind}");

        // Mark chunk dirty for navigation rebuild (ConnectivityVersion already incremented by SetTile)
        world.MarkChunkDirty(ck);

        // Vertical dirty propagation for ramp/stairs semantics (affects z and z±1)
        // Ensure ramp ascend/standable relationships are rebuilt consistently across layers
        if (ck.Z + 1 < world.MaxZ)
        {
            world.MarkChunkDirty(new World.ChunkKey(ck.ChunkX, ck.ChunkY, ck.Z + 1));
        }
        if (ck.Z - 1 >= 0)
        {
            world.MarkChunkDirty(new World.ChunkKey(ck.ChunkX, ck.ChunkY, ck.Z - 1));
        }

        // Cross-chunk propagation across XY edges for tiles at chunk borders (same Z)
        // If the edited tile lies on a chunk boundary, mark the adjacent neighbor chunk dirty
        int size = World.Chunk.SIZE_XY;
        int worldSizeChunks = world.SizeInChunks;

        // West neighbor
        if (lx == 0 && ck.ChunkX - 1 >= 0)
        {
            world.MarkChunkDirty(new World.ChunkKey(ck.ChunkX - 1, ck.ChunkY, ck.Z));
        }
        // East neighbor
        if (lx == size - 1 && ck.ChunkX + 1 < worldSizeChunks)
        {
            world.MarkChunkDirty(new World.ChunkKey(ck.ChunkX + 1, ck.ChunkY, ck.Z));
        }
        // North neighbor
        if (ly == 0 && ck.ChunkY - 1 >= 0)
        {
            world.MarkChunkDirty(new World.ChunkKey(ck.ChunkX, ck.ChunkY - 1, ck.Z));
        }
        // South neighbor
        if (ly == size - 1 && ck.ChunkY + 1 < worldSizeChunks)
        {
            world.MarkChunkDirty(new World.ChunkKey(ck.ChunkX, ck.ChunkY + 1, ck.Z));
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

        var oldPos = item.Position;
        var oldZ = item.Z;
        var newPos = new SadRogue.Primitives.Point(worldX, worldY);
        world.Items.UpdateItemPosition(item.Guid, oldPos, oldZ, newPos, worldZ);

        // Merge stacks at the destination after move to consolidate identical items
        try
        {
            int removed = world.Items.MergeStacksAt(newPos, worldZ);
            if (removed > 0)
            {
                LogCallback?.Invoke($"[DIFF][Items] MergeStacksAt ({worldX},{worldY},{worldZ}) removed={removed}");
            }
        }
        catch (Exception ex)
        {
            LogCallback?.Invoke($"[DIFF][Items] MergeStacksAt exception: {ex.Message}");
        }
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

        // After uncarrying at destination, attempt to merge with any stacks at the same tile
        try
        {
            int removed = world.Items.MergeStacksAt(item.Position, item.Z);
            if (removed > 0)
            {
                LogCallback?.Invoke($"[DIFF][Items] MergeStacksAt (uncarry) ({item.Position.X},{item.Position.Y},{item.Z}) removed={removed}");
            }
        }
        catch (Exception ex)
        {
            LogCallback?.Invoke($"[DIFF][Items] MergeStacksAt (uncarry) exception: {ex.Message}");
        }
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
