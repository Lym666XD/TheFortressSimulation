using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Time;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Mining planner: reads mining designations and produces PlannedDig DTOs.
/// Read phase only; no world mutation. Write phase hands off to executor inbox.
/// </summary>
public sealed class MiningSystem : ITick
{
    private readonly World.World _world;
    private readonly OrdersManager _orders;
    private readonly int _maxPerTick;

    private readonly List<PlannedDig> _planned = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<PlannedDig> _outbox = new();

    public MiningSystem(World.World world, OrdersManager orders, int maxPerTick = 128)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _maxPerTick = Math.Max(1, maxPerTick);
    }

    public int Priority => UpdateOrder.Priority.Items; // plan before unit jobs write; same as items stage
    public string SystemId => "Jobs.Mining";

    public void ReadTick(ulong tick)
    {
        _planned.Clear();

        // One-shot mode: drain designations, no persistent active set
        var desigs = new List<MiningDesignation>();
        _orders.DrainMiningDesignations(desigs, maxCount: 8);
        if (desigs.Count == 0) return;

        int plannedCount = 0;

        foreach (var d in desigs)
        {
            // Enumerate all cells in rect at Z in row-major, deterministic order
            for (int y = d.WorldRect.Y; y < d.WorldRect.MaxExtentY; y++)
            {
                for (int x = d.WorldRect.X; x < d.WorldRect.MaxExtentX; x++)
                {
                    if (plannedCount >= _maxPerTick) break;
                    var tileOpt = _world.GetTile(x, y, d.Z);
                    if (tileOpt == null) continue;
                    var tile = tileOpt.Value;

                    // Support both SolidWall and Ramp
                    if (tile.Kind != TerrainKind.SolidWall && tile.Kind != TerrainKind.Ramp) continue;

                    // Ramp requires standable adjacency (to avoid digging under your feet)
                    if (!HasStandableAdjacency(x, y, d.Z)) continue;

                    // Geology handle extraction: rely on tile.GeoMatId (ushort) if available; else 0
                    ushort geology = tile.GeoMatId;
                    byte terrainKind = (byte)tile.Kind;
                    var pd = new PlannedDig(new Point(x, y), d.Z, geology, terrainKind, d.Priority, SeedFrom(x, y, d.Z));
                    _planned.Add(pd);
                    plannedCount++;
                }
                if (plannedCount >= _maxPerTick) break;
            }
            if (plannedCount >= _maxPerTick) break;
        }
    }

    public void WriteTick(ulong tick)
    {
        if (_planned.Count == 0) return;
        foreach (var p in _planned)
            _outbox.Enqueue(p);
        _planned.Clear();
    }

    private bool HasStandableAdjacency(int x, int y, int z)
    {
        // Check N/E/S/W for standable tiles (open with floor or similar)
        static IEnumerable<(int dx,int dy)> Adj() { yield return (0,-1); yield return (1,0); yield return (0,1); yield return (-1,0); }
        foreach (var (dx, dy) in Adj())
        {
            var t = _world.GetTile(x + dx, y + dy, z);
            if (t == null) continue;
            if (t.Value.IsStandable) return true;
        }
        return false;
    }

    private static ulong SeedFrom(int x, int y, int z)
    {
        unchecked
        {
            uint s = 2166136261;
            s = (s ^ (uint)x) * 16777619;
            s = (s ^ (uint)y) * 16777619;
            s = (s ^ (uint)z) * 16777619;
            return s;
        }
    }

    public readonly record struct PlannedDig(Point Cell, int Z, ushort GeologyHandle, byte TerrainKind, int Priority, ulong Seed);

    /// <summary>
    /// Dequeue up to max planned digs for job creation.
    /// </summary>
    public int DequeuePlannedDigs(int max, IList<PlannedDig> into)
    {
        int n = 0;
        while (n < max && _outbox.TryDequeue(out var m))
        {
            into.Add(m);
            n++;
        }
        return n;
    }
}
