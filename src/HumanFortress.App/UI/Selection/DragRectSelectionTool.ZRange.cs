using SadRogue.Primitives;

namespace HumanFortress.App.UI.Selection;

internal sealed partial class DragRectSelectionTool
{
    public void AdjustZRange(int delta)
    {
        if (_state != State.Dragging) return;
        if (delta == 0) return;

        if (delta > 0)
            _zMax = Math.Min(_worldSizeTiles - 1, _zMax + 1);
        else
            _zMin = Math.Max(0, _zMin - 1);

        EnsureStartZInsideRange();
        Changed?.Invoke(Current);
    }

    public void SetZRangeEnd(int z)
    {
        if (_state != State.Dragging) return;

        int clampedZ = Math.Max(0, Math.Min(_worldSizeTiles - 1, z));
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
        int x = Math.Max(0, Math.Min(_worldSizeTiles - 1, p.X));
        int y = Math.Max(0, Math.Min(_worldSizeTiles - 1, p.Y));
        return new Point(x, y);
    }
}
