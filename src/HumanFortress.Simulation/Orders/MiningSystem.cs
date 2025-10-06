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
public enum MiningSegment { None, Top, Middle, Bottom }

public sealed class MiningSystem : ITick
{
    public static System.Action<string>? LogCallback;
    private readonly World.World _world;
    private readonly OrdersManager _orders;
    private readonly int _maxPerTick;

    private readonly List<PlannedDig> _planned = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<PlannedDig> _outbox = new();

    // Unified: persistent active designations (id -> cursor)
    private readonly Dictionary<int, ActiveDesignation> _active = new();
    // Persistent cancellation regions (RemoveDigging)
    private readonly List<OrdersManager.MiningCancelRegion> _cancels = new();

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

        // Drain new unified adds
        var adds = new List<OrdersManager.MiningDesignation>();
        int drainedAdds = _orders.DrainMiningAdds(adds, maxCount: 64);
        if (drainedAdds > 0)
        {
            var _msgA = "[MINING][PLAN] Adds drained: " + drainedAdds;
            if (LogCallback != null) LogCallback(_msgA); else System.Console.WriteLine(_msgA);
            foreach (var a in adds)
            {
                _active[a.Id] = new ActiveDesignation(a.Id, a.Rect, a.ZMin, a.ZMax, a.Action, a.Priority, a.CreatedTick);
                var _msgD = $"[MINING][PLAN] Designation id={a.Id} action={a.Action} rect={a.Rect} z={a.ZMin}..{a.ZMax} layers={a.ZMax - a.ZMin + 1}";
                if (LogCallback != null) LogCallback(_msgD); else System.Console.WriteLine(_msgD);
            }
        }

        // Drain cancellations
        var canc = new List<OrdersManager.MiningCancelRegion>();
        int drainedCanc = _orders.DrainMiningCancels(canc, maxCount: 64);
        if (drainedCanc > 0)
        {
            var _msgC = "[MINING][PLAN] Cancels drained: " + drainedCanc;
            if (LogCallback != null) LogCallback(_msgC); else System.Console.WriteLine(_msgC);
            _cancels.AddRange(canc);
        }

        if (_active.Count == 0) return;

        // Budgeted weighted round-robin by priority (desc)
        int budget = _maxPerTick;
        int producedTotal = 0;
        var actives = _active.Values.Where(a => !a.Done).OrderByDescending(a => a.Priority).ThenBy(a => a.Id).ToList();
        if (actives.Count == 0) return;

        // Per-id production counter this tick for logging
        var perId = new Dictionary<int, int>();

        bool progress;
        do
        {
            progress = false;
            foreach (var ad in actives)
            {
                if (budget <= 0) break;
                if (ad.Done) continue;
                var cursor = ad; // struct copy
                if (TryNextDigFrom(ref cursor, out var pd))
                {
                    _planned.Add(pd);
                    producedTotal++;
                    budget--;
                    progress = true;
                    if (!perId.ContainsKey(ad.Id)) perId[ad.Id] = 0;
                    perId[ad.Id]++;
                }
                _active[ad.Id] = cursor;
            }
            // refresh actives to skip completed ones in next round
            actives = _active.Values.Where(a => !a.Done).OrderByDescending(a => a.Priority).ThenBy(a => a.Id).ToList();
        } while (budget > 0 && progress && actives.Count > 0);

        // Log per id added counts
        if (perId.Count > 0)
        {
            foreach (var kv in perId)
            {
                var _msgP = "[MINING][PLAN] id=" + kv.Key + " planned+=" + kv.Value;
                if (LogCallback != null) LogCallback(_msgP); else System.Console.WriteLine(_msgP);
            }
        }
    }

    public void WriteTick(ulong tick)
    {
        if (_planned.Count == 0)
        {
            if ((tick % 60UL) == 0UL)
            {
                var _msg = "[MINING][PLAN] WriteTick: no planned digs";
                if (LogCallback != null) LogCallback(_msg); else System.Console.WriteLine(_msg);
            }
            return;
        }
        {
            var _msg2 = $"[MINING][PLAN] WriteTick: enqueuing planned digs: {_planned.Count}";
            if (LogCallback != null) LogCallback(_msg2); else System.Console.WriteLine(_msg2);
        }
        foreach (var p in _planned)
            _outbox.Enqueue(p);
        _planned.Clear();
    }

    // Drain advanced mining designations and expand into PlannedDig entries
        // Unified scanner: attempt to produce one PD from the given active designation
    private bool TryNextDigFrom(ref ActiveDesignation ad, out PlannedDig pd)
    {
        // advance until a qualifying cell or done
        int scannedCells = 0;
        int rejectedByFilter = 0;

        // For stairwells, scan from ZMax down to ZMin (reverse order to prioritize Top segments)
        // For other actions, scan from ZMin up to ZMax (normal order)
        bool isStairwell = ad.Action == MiningAction.DigStairwell;

        while (true)
        {
            // Check Z bounds
            if (isStairwell)
            {
                if (ad.CurZ < ad.ZMin) break;
            }
            else
            {
                if (ad.CurZ > ad.ZMax) break;
            }

            for (; ad.CurY < ad.Rect.MaxExtentY; ad.CurY++, ad.CurX = ad.Rect.X)
            {
                for (; ad.CurX < ad.Rect.MaxExtentX; ad.CurX++)
                {
                    // cancellation check
                    if (IsCanceled(ad.CurX, ad.CurY, ad.CurZ)) continue;

                    var tileOpt = _world.GetTile(ad.CurX, ad.CurY, ad.CurZ);
                    if (tileOpt == null) continue;
                    var tile = tileOpt.Value;
                    scannedCells++;
                    var cell = new SadRogue.Primitives.Point(ad.CurX, ad.CurY);
                    ushort geo = tile.GeoMatId;
                    byte tk = (byte)tile.Kind;

                    switch (ad.Action)
                    {
                        case MiningAction.Dig:
                            if (tile.Kind != TerrainKind.SolidWall && tile.Kind != TerrainKind.Ramp) break;
                            if (tile.Kind == TerrainKind.Ramp && !HasStandableAdjacency(ad.CurX, ad.CurY, ad.CurZ)) break;
                            pd = new PlannedDig(cell, ad.CurZ, geo, tk, ad.Priority, SeedFrom(ad.CurX, ad.CurY, ad.CurZ), MiningAction.Dig, MiningSegment.None, ad.Id);
                            AdvanceCursor(ref ad);
                            return true;
                        case MiningAction.DigRamp:
                            if (tile.Kind != TerrainKind.SolidWall) break;
                            pd = new PlannedDig(cell, ad.CurZ, geo, tk, ad.Priority, SeedFrom(ad.CurX, ad.CurY, ad.CurZ), MiningAction.DigRamp, MiningSegment.None, ad.Id);
                            AdvanceCursor(ref ad);
                            return true;
                        case MiningAction.DigChannel:
                            if (tile.Kind == TerrainKind.OpenNoFloor) break;
                            pd = new PlannedDig(cell, ad.CurZ, geo, tk, ad.Priority, SeedFrom(ad.CurX, ad.CurY, ad.CurZ), MiningAction.DigChannel, MiningSegment.None, ad.Id);
                            AdvanceCursor(ref ad);
                            return true;
                        case MiningAction.DigStairwell:
                            // Skip single-layer stairwells (UI already showed toast)
                            if (ad.ZMin == ad.ZMax) break;
                            // Debug: log every tile scanned for stairwell
                            if (scannedCells <= 10)
                            {
                                var _msgScan = $"[MINING][PLAN] Stairwell id={ad.Id} scan cell ({ad.CurX},{ad.CurY},{ad.CurZ}) kind={tile.Kind} ZMin={ad.ZMin} ZMax={ad.ZMax}";
                                if (LogCallback != null) LogCallback(_msgScan); else System.Console.WriteLine(_msgScan);
                            }
                            // Stairwells can dig through SolidWall (rock) or on OpenWithFloor (existing floor)
                            // Skip only air (OpenNoFloor) - can't build stairs in mid-air
                            if (tile.Kind == TerrainKind.OpenNoFloor)
                            {
                                if (scannedCells <= 5) // only log first few rejections
                                {
                                    var _msg = $"[MINING][PLAN] Stairwell id={ad.Id} reject cell ({ad.CurX},{ad.CurY},{ad.CurZ}) kind={tile.Kind} (can't dig stairs in air)";
                                    if (LogCallback != null) LogCallback(_msg); else System.Console.WriteLine(_msg);
                                }
                                rejectedByFilter++;
                                break;
                            }
                            // Stairwell segment: Top=highest, Bottom=lowest, Middle=between
                            // But allow digging in ANY order (user can dig bottom-up or top-down)
                            var seg = (ad.CurZ == ad.ZMax) ? MiningSegment.Top : (ad.CurZ == ad.ZMin ? MiningSegment.Bottom : MiningSegment.Middle);
                            pd = new PlannedDig(cell, ad.CurZ, geo, tk, ad.Priority, SeedFrom(ad.CurX, ad.CurY, ad.CurZ), MiningAction.DigStairwell, seg, ad.Id);
                            var _msgPD = $"[MINING][PLAN] Stairwell id={ad.Id} PRODUCE PlannedDig at ({ad.CurX},{ad.CurY},{ad.CurZ}) seg={seg}";
                            if (LogCallback != null) LogCallback(_msgPD); else System.Console.WriteLine(_msgPD);
                            AdvanceCursor(ref ad);
                            return true;
                        default:
                            break;
                    }
                }
            }

            // Y loop完成但没有找到符合条件的tile，推进到下一个Z层
            if (ad.CurY >= ad.Rect.MaxExtentY)
            {
                ad.CurY = ad.Rect.Y;
                ad.CurX = ad.Rect.X;
                if (isStairwell)
                    ad.CurZ--;
                else
                    ad.CurZ++;
            }
        }
        if (rejectedByFilter > 0)
        {
            var _msgDone = $"[MINING][PLAN] Stairwell id={ad.Id} done: scanned={scannedCells} rejected={rejectedByFilter} (all non-SolidWall)";
            if (LogCallback != null) LogCallback(_msgDone); else System.Console.WriteLine(_msgDone);
        }
        ad.MarkDone();
        pd = default;
        return false;
    }

    private bool IsCanceled(int x, int y, int z)
    {
        if (_cancels.Count == 0) return false;
        var p = new SadRogue.Primitives.Point(x, y);
        foreach (var c in _cancels)
        {
            if (z < c.ZMin || z > c.ZMax) continue;
            if (c.Rect.Contains(p)) return true;
        }
        return false;
    }

    private static void AdvanceCursor(ref ActiveDesignation ad)
    {
        ad.CurX++;
        if (ad.CurX >= ad.Rect.MaxExtentX)
        {
            ad.CurX = ad.Rect.X;
            ad.CurY++;
        }
        if (ad.CurY >= ad.Rect.MaxExtentY)
        {
            ad.CurY = ad.Rect.Y;
            // Stairwells scan downward (Z decreases), others scan upward (Z increases)
            if (ad.Action == HumanFortress.Simulation.Orders.MiningAction.DigStairwell)
                ad.CurZ--;
            else
                ad.CurZ++;
        }
        // Check done: stairwell done when CurZ < ZMin, others done when CurZ > ZMax
        if (ad.Action == HumanFortress.Simulation.Orders.MiningAction.DigStairwell)
        {
            if (ad.CurZ < ad.ZMin) ad.MarkDone();
        }
        else
        {
            if (ad.CurZ > ad.ZMax) ad.MarkDone();
        }
    }

    private bool HasStandableAdjacency(int x, int y, int z)
    {
        bool Acceptable(int tx, int ty)
        {
            var t = _world.GetTile(tx, ty, z);
            return t != null && t.Value.IsWalkable; // match Jobs-side Acceptable
        }
        // Prefer NESW
        if (Acceptable(x, y - 1) || Acceptable(x + 1, y) || Acceptable(x, y + 1) || Acceptable(x - 1, y))
            return true;
        // Allow diagonals (match Jobs behaviour)
        if (Acceptable(x + 1, y - 1) || Acceptable(x + 1, y + 1) || Acceptable(x - 1, y + 1) || Acceptable(x - 1, y - 1))
            return true;
        // Expand search radius slightly (r=2..3) along ring edges (match Jobs)
        for (int r = 2; r <= 3; r++)
        {
            for (int yy = y - r; yy <= y + r; yy++)
            {
                int xx1 = x - r; int xx2 = x + r;
                if (Acceptable(xx1, yy) || Acceptable(xx2, yy)) return true;
            }
            for (int xx = x - r + 1; xx <= x + r - 1; xx++)
            {
                int yy1 = y - r; int yy2 = y + r;
                if (Acceptable(xx, yy1) || Acceptable(xx, yy2)) return true;
            }
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

    public readonly record struct PlannedDig(Point Cell, int Z, ushort GeologyHandle, byte TerrainKind, int Priority, ulong Seed, MiningAction Action, MiningSegment Segment, int DesignationId);

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

internal struct ActiveDesignation
{
    public int Id;
    public SadRogue.Primitives.Rectangle Rect;
    public int ZMin;
    public int ZMax;
    public int CurZ;
    public int CurX;
    public int CurY;
    public HumanFortress.Simulation.Orders.MiningAction Action;
    public int Priority;
    public ulong CreatedTick;
    private bool _done;
    public bool Done => _done;
    public ActiveDesignation(int id, SadRogue.Primitives.Rectangle rect, int zMin, int zMax, HumanFortress.Simulation.Orders.MiningAction action, int priority, ulong createdTick)
    {
        Id = id;
        Rect = rect;
        ZMin = Math.Min(zMin, zMax);
        ZMax = Math.Max(zMin, zMax);
        // For stairwells, start from ZMax (top layer) to generate Top segments first
        // For other actions, start from ZMin
        CurZ = (action == HumanFortress.Simulation.Orders.MiningAction.DigStairwell) ? ZMax : ZMin;
        CurX = rect.X;
        CurY = rect.Y;
        Action = action;
        Priority = priority;
        CreatedTick = createdTick;
        _done = false;
    }
    public void MarkDone() { _done = true; }
}