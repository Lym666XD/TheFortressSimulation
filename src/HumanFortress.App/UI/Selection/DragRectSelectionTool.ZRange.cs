using SadRogue.Primitives;

namespace HumanFortress.App.UI.Selection;

internal sealed partial class DragRectSelectionTool
{
    public void AdjustZRange(int delta)
    {
        if (_state != State.Dragging) return;
        if (delta == 0) return;

        if (delta > 0)
            _zMax = Math.Min(_worldBounds.MaxZExclusive - 1, _zMax + 1);
        else
            _zMin = Math.Max(_worldBounds.MinZ, _zMin - 1);

        EnsureStartZInsideRange();
        Changed?.Invoke(Current);
    }

    public void SetZRangeEnd(int z)
    {
        if (_state != State.Dragging) return;

        int clampedZ = ClampZ(z);
        _zMin = Math.Min(_zStart, clampedZ);
        _zMax = Math.Max(_zStart, clampedZ);
        Changed?.Invoke(Current);
    }

    private void EnsureStartZInsideRange()
    {
        if (_zStart < _zMin) _zMin = _zStart;
        if (_zStart > _zMax) _zMax = _zStart;
    }

    private Point Clamp(Point p)
    {
        int x = Math.Clamp(p.X, _worldBounds.MinX, _worldBounds.MaxXExclusive - 1);
        int y = Math.Clamp(p.Y, _worldBounds.MinY, _worldBounds.MaxYExclusive - 1);
        return new Point(x, y);
    }

    private int ClampZ(int z)
    {
        return Math.Clamp(z, _worldBounds.MinZ, _worldBounds.MaxZExclusive - 1);
    }
}
