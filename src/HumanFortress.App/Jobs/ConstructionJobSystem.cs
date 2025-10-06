using System;
using System.Collections.Generic;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.Orders;

namespace HumanFortress.App.Jobs;

/// <summary>
/// Executes structural construction (L0). Consumes PlannedBuild from ConstructionSystem and emits DiffOps.
/// Ghost removal is TODO (requires placeable index by GUID or position lookup helpers).
/// </summary>
public sealed class ConstructionJobSystem : ITick
{
    private readonly HumanFortress.Simulation.World.World _world;
    private readonly ConstructionSystem _planner;
    private readonly DiffLog? _diff;
    private readonly List<PlannedBuild> _buffer = new();
    private readonly int _maxPerTick;

    public ConstructionJobSystem(HumanFortress.Simulation.World.World world, ConstructionSystem planner, DiffLog? diffLog = null, int maxPerTick = 256)
    {
        _world = world;
        _planner = planner;
        _diff = diffLog;
        _maxPerTick = Math.Max(1, maxPerTick);
    }

    public int Priority => UpdateOrder.Priority.WorldTerrain;
    public string SystemId => "Jobs.Construction";

    public void ReadTick(ulong tick)
    {
        _buffer.Clear();
        _planner.DequeuePlannedBuilds(_maxPerTick, _buffer);
    }

    public void WriteTick(ulong tick)
    {
        if (_buffer.Count == 0 || _diff == null) return;
        foreach (var b in _buffer)
        {
            // Encode target
            int chunkX = b.Cell.X / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int chunkY = b.Cell.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int lx = b.Cell.X % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int ly = b.Cell.Y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int localIndex = HumanFortress.Simulation.World.Chunk.LocalIndex(lx, ly);
            int chunkId = EncodeChunkId(new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, b.Z));
            var target = new DiffTarget(chunkId, localIndex);

            // Pack args with kind + geology handle
            ulong args = ConstructionSystem.PackSetTerrainArgs(b.TargetKind, b.GeologyHandle);
            var op = new DiffOp(DiffOpType.SetTerrain, target, SystemId, UpdateOrder.Priority.WorldTerrain, args);
            _diff.AddOp(op);

            // Remove ghost at anchor immediately (L2), purpose = shape name
            try
            {
                HumanFortress.Simulation.Placeables.PlaceableManager.RemoveGhostAt(_world, b.Cell, b.Z, b.Shape.ToString(), tick);
            }
            catch { }
        }
        _buffer.Clear();
    }

    private static int EncodeChunkId(HumanFortress.Simulation.World.ChunkKey ck)
    {
        // [z:10][x:10][y:10]
        return ((ck.Z & 0x3FF) << 20) | ((ck.ChunkX & 0x3FF) << 10) | (ck.ChunkY & 0x3FF);
    }
}
