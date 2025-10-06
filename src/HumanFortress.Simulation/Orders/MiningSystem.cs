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

    // Persistent active set to avoid losing interior tiles when exceeding per-tick budget.
    private readonly List<ActiveRect> _active = new();

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

        // Move new designations into persistent active set
        var desigs = new List<MiningDesignation>();
        _orders.DrainMiningDesignations(desigs, maxCount: 16);
        foreach (var d in desigs)
        {
            _active.Add(new ActiveRect(d.WorldRect, d.Z, d.Priority));
        }

        // Process advanced designations as-needed (emits directly to _planned)
        int plannedCount = 0;
        ProcessAdvancedDesignations(_maxPerTick, ref plannedCount);

        if (_active.Count == 0)
        {
            return;
        }

        // Continue producing from persistent active rects
        // Round-robin across active rects to fairly drain
        int idx = 0;
        while (plannedCount < _maxPerTick && _active.Count > 0 && idx < _active.Count)
        {
            var ar = _active[idx];
            int emittedThisRect = 0;
            while (plannedCount < _maxPerTick && TryNextDigFrom(ref ar, out var pd))
            {
                _planned.Add(pd);
                plannedCount++;
                emittedThisRect++;
            }

            if (ar.Done)
            {
                _active.RemoveAt(idx); // do not advance idx
            }
            else
            {
                _active[idx] = ar;
                idx++; // move to next rect
            }
        }
    }

    public void WriteTick(ulong tick)
    {
        if (_planned.Count == 0) return;
        foreach (var p in _planned)
            _outbox.Enqueue(p);
        _planned.Clear();
    }

    // Drain advanced mining designations and expand into PlannedDig entries
    private void ProcessAdvancedDesignations(int budget, ref int plannedCount)
    {
        var advList = new List<MiningAdvancedDesignation>();
        _orders.DrainMiningAdvanced(advList, maxCount: 8);
        foreach (var adv in advList)
        {
            if (plannedCount >= budget) break;
            switch (adv.Action)
            {
                case MiningAction.DigRamp:
                    // Only plan for SolidWall at each z in range
                    for (int z = adv.ZMin; z <= adv.ZMax && plannedCount < budget; z++)
                    {
                        for (int y = adv.Rect.Y; y < adv.Rect.MaxExtentY && plannedCount < budget; y++)
                        for (int x = adv.Rect.X; x < adv.Rect.MaxExtentX && plannedCount < budget; x++)
                        {
                            var t = _world.GetTile(x, y, z);
                            if (t == null) continue;
                            if (t.Value.Kind != TerrainKind.SolidWall) continue;
                            ushort geo = t.Value.GeoMatId;
                            byte tk = (byte)t.Value.Kind;
                            _planned.Add(new PlannedDig(new Point(x, y), z, geo, tk, adv.Priority, SeedFrom(x,y,z), MiningAction.DigRamp, MiningSegment.None));
                            plannedCount++;
                        }
                    }
                    break;
                case MiningAction.DigChannel:
                    // Only plan for tiles with floor (OpenWithFloor) at each z
                    for (int z = adv.ZMin; z <= adv.ZMax && plannedCount < budget; z++)
                    {
                        for (int y = adv.Rect.Y; y < adv.Rect.MaxExtentY && plannedCount < budget; y++)
                        for (int x = adv.Rect.X; x < adv.Rect.MaxExtentX && plannedCount < budget; x++)
                        {
                            var t = _world.GetTile(x, y, z);
                            if (t == null) continue;
                            if (t.Value.Kind != TerrainKind.OpenWithFloor) continue;
                            ushort geo = t.Value.GeoMatId;
                            byte tk = (byte)t.Value.Kind;
                            _planned.Add(new PlannedDig(new Point(x, y), z, geo, tk, adv.Priority, SeedFrom(x,y,z), MiningAction.DigChannel, MiningSegment.None));
                            plannedCount++;
                        }
                    }
                    break;
                case MiningAction.DigStairwell:
                    // Only over OpenWithFloor at top; emit per-level segments
                    for (int y = adv.Rect.Y; y < adv.Rect.MaxExtentY && plannedCount < budget; y++)
                    for (int x = adv.Rect.X; x < adv.Rect.MaxExtentX && plannedCount < budget; x++)
                    {
                        var top = _world.GetTile(x, y, adv.ZMin);
                        if (top == null || top.Value.Kind != TerrainKind.OpenWithFloor) continue;
                        int zMin = adv.ZMin;
                        int zMax = adv.ZMax;
                        if (zMin > zMax) { var tmp=zMin; zMin=zMax; zMax=tmp; }
                        for (int z = zMin; z <= zMax && plannedCount < budget; z++)
                        {
                            var t = _world.GetTile(x, y, z);
                            if (t == null) continue;
                            ushort geo = t.Value.GeoMatId;
                            byte tk = (byte)t.Value.Kind;
                            MiningSegment seg = (z==zMin) ? MiningSegment.Top : (z==zMax ? MiningSegment.Bottom : MiningSegment.Middle);
                            _planned.Add(new PlannedDig(new Point(x, y), z, geo, tk, adv.Priority, SeedFrom(x,y,z), MiningAction.DigStairwell, seg));
                            plannedCount++;
                        }
                    }
                    break;
                default:
                    // Fallback: degrade to simple Dig on walls within rect for each z
                    for (int z = adv.ZMin; z <= adv.ZMax && plannedCount < budget; z++)
                    {
                        for (int y = adv.Rect.Y; y < adv.Rect.MaxExtentY && plannedCount < budget; y++)
                        for (int x = adv.Rect.X; x < adv.Rect.MaxExtentX && plannedCount < budget; x++)
                        {
                            var t = _world.GetTile(x, y, z);
                            if (t == null) continue;
                            if (t.Value.Kind != TerrainKind.SolidWall && t.Value.Kind != TerrainKind.Ramp) continue;
                            ushort geo = t.Value.GeoMatId;
                            byte tk = (byte)t.Value.Kind;
                            _planned.Add(new PlannedDig(new Point(x, y), z, geo, tk, adv.Priority, SeedFrom(x,y,z), MiningAction.Dig, MiningSegment.None));
                            plannedCount++;
                        }
                    }
                    break;
            }
        }
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

    private bool TryNextDigFrom(ref ActiveRect ar, out PlannedDig pd)
    {
        // Row-major scan from cursor; emit a PD when hitting a SolidWall or (Ramp with adjacency)
        for (; ar.CurY < ar.Rect.MaxExtentY; ar.CurY++, ar.CurX = ar.Rect.X)
        {
            for (; ar.CurX < ar.Rect.MaxExtentX; ar.CurX++)
            {
                var tileOpt = _world.GetTile(ar.CurX, ar.CurY, ar.Z);
                if (tileOpt == null) continue;
                var tile = tileOpt.Value;

                if (tile.Kind != TerrainKind.SolidWall && tile.Kind != TerrainKind.Ramp) continue;
                if (tile.Kind == TerrainKind.Ramp && !HasStandableAdjacency(ar.CurX, ar.CurY, ar.Z)) continue;

                ushort geology = tile.GeoMatId;
                byte terrainKind = (byte)tile.Kind;
                pd = new PlannedDig(new Point(ar.CurX, ar.CurY), ar.Z, geology, terrainKind, ar.Priority, SeedFrom(ar.CurX, ar.CurY, ar.Z), MiningAction.Dig, MiningSegment.None);

                // Advance cursor past this cell
                ar.CurX++;
                if (ar.CurX >= ar.Rect.MaxExtentX)
                {
                    ar.CurX = ar.Rect.X;
                    ar.CurY++;
                }
                if (ar.CurY >= ar.Rect.MaxExtentY) ar.MarkDone();
                return true;
            }
        }
        ar.MarkDone();
        pd = default;
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

    public readonly record struct PlannedDig(Point Cell, int Z, ushort GeologyHandle, byte TerrainKind, int Priority, ulong Seed, MiningAction Action, MiningSegment Segment);

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

internal struct ActiveRect
{
    public SadRogue.Primitives.Rectangle Rect;
    public int Z;
    public int Priority;
    public int CurX;
    public int CurY;
    private bool _done;
    public bool Done => _done;
    public ActiveRect(SadRogue.Primitives.Rectangle rect, int z, int priority)
    {
        Rect = rect;
        Z = z;
        Priority = priority;
        CurX = rect.X;
        CurY = rect.Y;
        _done = false;
    }
    public void MarkDone() { _done = true; }
}
