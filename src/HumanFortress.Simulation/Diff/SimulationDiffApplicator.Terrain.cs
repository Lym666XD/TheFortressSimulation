using System;
using System.Linq;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.Topology;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using SimulationChunk = HumanFortress.Simulation.World.Chunk;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Diff;

internal static partial class SimulationDiffApplicator
{
    private static void ApplySetTerrain(
        SimulationWorld world,
        DiffOp op,
        IRuntimeGeologyCatalog? geologyCatalog,
        ulong currentTick)
    {
        var (ck, lx, ly) = DecodeTarget(op.Target);
        var chunk = world.GetChunk(ck);
        if (chunk == null)
            throw new InvalidOperationException($"Terrain target chunk {ck} is not loaded.");

        var tile = chunk.GetTile(lx, ly);
        var kindVal = (byte)(op.Args & 0xFF);
        var newKind = (TerrainKind)kindVal;
        ushort overrideGeo = (ushort)((op.Args >> 8) & 0xFFFF);

        var bits = TerrainBitOps.SetKind(tile.TerrainBits, newKind);
        if (op.SystemId.StartsWith("Orders.Construction", StringComparison.Ordinal) ||
            op.SystemId.StartsWith("Jobs.Construction", StringComparison.Ordinal))
        {
            bits = TerrainBitOps.SetNatural(bits, false);
            bool modifiable = ck.Z > 0;
            bits = TerrainBitOps.SetModifiable(bits, modifiable);
        }

        ushort newGeoHandle = tile.GeoMatId;
        if (overrideGeo != 0)
        {
            newGeoHandle = overrideGeo;
        }
        else
        {
            var geo = geologyCatalog?.GetGeologyByHandle(tile.GeoMatId);
            if (geo != null && geologyCatalog!.TryGetGeologyHandleByMaterialAndKind(geo.Material, newKind.ToString(), out var handle))
                newGeoHandle = handle;
        }

        byte newSurface = tile.SurfaceBits;
        if (newKind == TerrainKind.OpenNoFloor)
            newSurface = 0;

        var newTile = new TileBase(
            newGeoHandle,
            bits,
            newSurface,
            tile.FluidKind,
            tile.FluidDepth,
            tile.MetaBits,
            tile.TrafficCost);

        TopologyChangeTransaction.ApplyTerrain(world, ck, lx, ly, newTile, currentTick);

        int worldX = ck.ChunkX * SimulationChunk.SIZE_XY + lx;
        int worldY = ck.ChunkY * SimulationChunk.SIZE_XY + ly;
        Emit(world, $"[DIFF] ApplySetTerrain at ({worldX},{worldY},{ck.Z}): {tile.Kind} -> {newKind}");

        EjectOccupantsFromBlockedTerrain(world, worldX, worldY, ck.Z, newKind, currentTick);
    }

    private static void EjectOccupantsFromBlockedTerrain(
        SimulationWorld world,
        int worldX,
        int worldY,
        int worldZ,
        TerrainKind newKind,
        ulong currentTick)
    {
        if (IsWalkableTerrain(newKind))
            return;

        var stuck = world.Creatures
                .GetAllInstances()
                .Where(c => c.Z == worldZ && c.Position.X == worldX && c.Position.Y == worldY)
                .ToList();
            foreach (var creature in stuck)
            {
                var safe = WorldSafetyQueries.FindNearestStandableNonConstructionSite(world, worldX, worldY, worldZ, 3);
                if (safe == null)
                    continue;

                creature.Position = new Point(safe.Value.X, safe.Value.Y);
                creature.Z = safe.Value.Z;
                Emit(world, $"[EJECT] creature={creature.Guid} from=({worldX},{worldY},{worldZ}) to=({safe.Value.X},{safe.Value.Y},{safe.Value.Z})");
            }

            var items = world.Items.GetGroundItemsAt(new Point(worldX, worldY), worldZ).ToList();
            foreach (var item in items)
            {
                var safe = WorldSafetyQueries.FindNearestStandableNonConstructionSite(world, worldX, worldY, worldZ, 3);
                if (safe == null)
                    continue;

                var safePoint = new Point(safe.Value.X, safe.Value.Y);
                world.Items.UpdateItemPosition(item.Guid, safePoint, safe.Value.Z);
                world.Items.MergeStacksAt(safePoint, safe.Value.Z, currentTick);
                Emit(world, $"[EJECT] item={item.Guid} from=({worldX},{worldY},{worldZ}) to=({safe.Value.X},{safe.Value.Y},{safe.Value.Z})");
            }
    }

    private static bool IsWalkableTerrain(TerrainKind kind)
    {
        return kind == TerrainKind.OpenWithFloor
            || kind == TerrainKind.Ramp
            || kind == TerrainKind.StairsUp
            || kind == TerrainKind.StairsDown
            || kind == TerrainKind.StairsUD;
    }

}
