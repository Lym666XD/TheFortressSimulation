using SadRogue.Primitives;

namespace HumanFortress.App.UI.Selection;

public sealed class DragRectSelectionTool : ISelectionTool
{
    private enum State { Idle, Dragging }
    private State _state = State.Idle;
    private Point _start;
    private Point _current;
    private int _zStart;
    private int _zMin;
    private int _zMax;
    private readonly int _worldSizeTiles;

    public DragRectSelectionTool(int worldSizeTiles)
    {
        _worldSizeTiles = Math.Max(1, worldSizeTiles);
    }

    public bool IsActive => _state == State.Dragging;
    public Selection3D Current => _state == State.Dragging
        ? Selection3D.FromCorners(_start, _current, _zMin, _zMax)
        : default;

    public event Action<Selection3D>? Started;
    public event Action<Selection3D>? Changed;
    public event Action<Selection3D>? Completed;
    public event Action? Canceled;

    public bool Begin(Point worldStart, int z)
    {
        if (_state != State.Idle) return false;
        _start = Clamp(worldStart);
        _current = _start;
        _zStart = z;
        _zMin = z;
        _zMax = z;
        _state = State.Dragging;
        Started?.Invoke(Current);
        return true;
    }

    public void Update(Point worldCurrent)
    {
        if (_state != State.Dragging) return;
        _current = Clamp(worldCurrent);
        Changed?.Invoke(Current);
    }

    public Selection3D Complete()
    {
        if (_state != State.Dragging) return default;
        var s = Current;
        _state = State.Idle;
        Completed?.Invoke(s);
        return s;
    }

    public void Cancel()
    {
        if (_state == State.Dragging)
        {
            _state = State.Idle;
            Canceled?.Invoke();
        }
    }

    public void AdjustZRange(int delta)
    {
        if (_state != State.Dragging) return;
        if (delta == 0) return;
        // Symmetric expand around start: keep start inside [zMin..zMax]
        if (delta > 0)
        {
            _zMax = Math.Min(_worldSizeTiles - 1, _zMax + 1);
        }
        else
        {
            _zMin = Math.Max(0, _zMin - 1);
        }
        // Ensure start stays inside
        if (_zStart < _zMin) _zMin = _zStart;
        if (_zStart > _zMax) _zMax = _zStart;
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

    private Point Clamp(Point p)
    {
        int x = Math.Max(0, Math.Min(_worldSizeTiles - 1, p.X));
        int y = Math.Max(0, Math.Min(_worldSizeTiles - 1, p.Y));
        return new Point(x, y);
    }
}
