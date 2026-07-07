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
    private Point? _selectedTarget;

    internal Point? SelectedTarget => _selectedTarget;
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
