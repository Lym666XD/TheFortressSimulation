using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

/// <summary>
/// Debug overlay for visualizing navigation data.
/// </summary>
internal sealed partial class NavigationOverlay
{
    internal enum OverlayMode
    {
        None,
        Walkability,
        MovementCost,
        Traffic,
        Connectivity,
        PathDisplay,
        FlowField,
        RampMask,
    }

    private OverlayMode _currentMode = OverlayMode.None;
    private SimulationNavigationPathData _currentPath = SimulationNavigationPathData.Unavailable;
    private SimulationNavigationPathRequestData? _pendingPathRequest;
    private bool _awaitingPathResult;
    private Point? _selectedTarget;

    internal Point? SelectedTarget => _selectedTarget;
    internal SimulationNavigationPathRequestData? PendingPathRequest => _pendingPathRequest;
    internal SimulationNavigationOverlayMode SnapshotMode => ToSnapshotMode(_currentMode);

    internal NavigationOverlay()
    {
    }

    internal OverlayMode CurrentMode
    {
        get => _currentMode;
        set { _currentMode = value; }
    }

    internal void SetPath(SimulationNavigationPathData path)
    {
        _currentPath = path;
    }

    internal void ClearPath()
    {
        _currentPath = SimulationNavigationPathData.Unavailable;
        _pendingPathRequest = null;
        _awaitingPathResult = false;
    }

    internal void RequestPath(
        Point start,
        int startZ,
        Point destination,
        int destinationZ)
    {
        _pendingPathRequest = new SimulationNavigationPathRequestData(
            new HumanFortress.Contracts.Runtime.RuntimePoint(start.X, start.Y),
            startZ,
            new HumanFortress.Contracts.Runtime.RuntimePoint(destination.X, destination.Y),
            destinationZ);
        _awaitingPathResult = true;
    }

    internal bool TryApplyCommittedPath(SimulationNavigationPathFrameData frame)
    {
        if (!_awaitingPathResult
            || !_pendingPathRequest.HasValue
            || !frame.IsAvailable
            || frame.Request != _pendingPathRequest)
        {
            return false;
        }

        _currentPath = frame.Path;
        _awaitingPathResult = false;
        return true;
    }

    internal void SetTarget(Point target)
    {
        _selectedTarget = target;
    }

    internal void CycleMode() { var modes = Enum.GetValues<OverlayMode>(); int i = Array.IndexOf(modes, _currentMode); CurrentMode = modes[(i + 1) % modes.Length]; }

    private static SimulationNavigationOverlayMode ToSnapshotMode(OverlayMode mode)
    {
        return mode switch
        {
            OverlayMode.None => SimulationNavigationOverlayMode.None,
            OverlayMode.Walkability => SimulationNavigationOverlayMode.Walkability,
            OverlayMode.MovementCost => SimulationNavigationOverlayMode.MovementCost,
            OverlayMode.Traffic => SimulationNavigationOverlayMode.Traffic,
            OverlayMode.Connectivity => SimulationNavigationOverlayMode.Connectivity,
            OverlayMode.PathDisplay => SimulationNavigationOverlayMode.PathDisplay,
            OverlayMode.FlowField => SimulationNavigationOverlayMode.FlowField,
            OverlayMode.RampMask => SimulationNavigationOverlayMode.RampMask,
            _ => SimulationNavigationOverlayMode.None,
        };
    }
}
