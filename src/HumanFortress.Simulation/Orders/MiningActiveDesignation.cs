using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

internal struct ActiveDesignation
{
    internal int Id;
    internal Rectangle Rect;
    internal int ZMin;
    internal int ZMax;
    internal int CurZ;
    internal int CurX;
    internal int CurY;
    internal MiningAction Action;
    internal int Priority;
    internal ulong CreatedTick;

    private bool _done;

    internal bool Done => _done;

    internal ActiveDesignation(
        int id,
        Rectangle rect,
        int zMin,
        int zMax,
        MiningAction action,
        int priority,
        ulong createdTick)
    {
        Id = id;
        Rect = rect;
        ZMin = Math.Min(zMin, zMax);
        ZMax = Math.Max(zMin, zMax);
        CurZ = action == MiningAction.DigStairwell ? ZMax : ZMin;
        CurX = rect.X;
        CurY = rect.Y;
        Action = action;
        Priority = priority;
        CreatedTick = createdTick;
        _done = false;
    }

    internal void MarkDone()
    {
        _done = true;
    }
}
