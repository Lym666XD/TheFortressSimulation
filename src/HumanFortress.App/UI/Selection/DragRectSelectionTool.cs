using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.UI.Selection;

internal sealed partial class DragRectSelectionTool : ISelectionTool
{
    private enum State { Idle, Dragging }
    private State _state = State.Idle;
    private Point _start;
    private Point _current;
    private int _zStart;
    private int _zMin;
    private int _zMax;
    private RuntimeWorldBounds _worldBounds;

    public DragRectSelectionTool(int worldSizeTiles)
        : this(new RuntimeWorldBounds(
            0,
            0,
            Math.Max(1, worldSizeTiles),
            Math.Max(1, worldSizeTiles),
            0,
            Math.Max(1, worldSizeTiles)))
    {
    }

    public DragRectSelectionTool(RuntimeWorldBounds worldBounds)
    {
        SetWorldBounds(worldBounds);
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
        _zStart = ClampZ(z);
        _zMin = _zStart;
        _zMax = _zStart;
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

    public void SetWorldBounds(RuntimeWorldBounds worldBounds)
    {
        _worldBounds = worldBounds.IsEmpty
            ? new RuntimeWorldBounds(0, 0, 1, 1, 0, 1)
            : worldBounds;
        _start = Clamp(_start);
        _current = Clamp(_current);
        _zStart = ClampZ(_zStart);
        _zMin = ClampZ(_zMin);
        _zMax = ClampZ(_zMax);
        if (_zMin > _zMax)
            (_zMin, _zMax) = (_zMax, _zMin);
    }

}
