using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.Navigation;
using HumanFortress.Simulation.World;
using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed class FortressNavigationDebugController
{
    private Point? _pathStart;
    private int _pathStartZ;
    private WorldNavigationView? _navView;
    private World? _viewWorld;
    private NavigationManager? _viewNavigationManager;

    public bool HandleKeyboard(
        Keyboard keyboard,
        NavigationOverlay? navigationOverlay,
        NavigationManager? navigationManager,
        NavigationTuning? navigationTuning,
        World? world,
        Point cursorPosition,
        int currentZ,
        UiStore ui,
        ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        if (keyboard.IsKeyPressed(Keys.F9))
        {
            CycleNavigationOverlay(navigationOverlay, cursorPosition, ui, uiTick);
            return true;
        }

        if (!keyboard.IsKeyPressed(Keys.F10))
            return false;

        bool ctrl = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        if (ctrl)
        {
            navigationOverlay?.ClearPath();
            _pathStart = null;
            ui.AddToast("Path cleared", uiTick + 120);
            return true;
        }

        HandlePathTool(navigationOverlay, navigationManager, navigationTuning, world, cursorPosition, currentZ, ui, uiTick);
        return true;
    }

    private static void CycleNavigationOverlay(NavigationOverlay? navigationOverlay, Point cursorPosition, UiStore ui, ulong uiTick)
    {
        if (navigationOverlay == null)
        {
            ui.AddToast("Overlay: unavailable", uiTick + 150);
            return;
        }

        navigationOverlay.CycleMode();
        if (navigationOverlay.CurrentMode == NavigationOverlay.OverlayMode.FlowField)
            navigationOverlay.SetTarget(cursorPosition);

        ui.AddToast($"Overlay: {navigationOverlay.CurrentMode}", uiTick + 150);
    }

    private void HandlePathTool(
        NavigationOverlay? navigationOverlay,
        NavigationManager? navigationManager,
        NavigationTuning? navigationTuning,
        World? world,
        Point cursorPosition,
        int currentZ,
        UiStore ui,
        ulong uiTick)
    {
        if (world == null || navigationManager == null || navigationOverlay == null)
            return;

        var navView = GetNavigationView(navigationManager, world);
        if (_pathStart == null)
        {
            _pathStart = cursorPosition;
            _pathStartZ = currentZ;
            ui.AddToast($"Start set at ({cursorPosition.X},{cursorPosition.Y},{currentZ})", uiTick + 150);
            navigationOverlay.CurrentMode = NavigationOverlay.OverlayMode.PathDisplay;
            return;
        }

        var tuning = navigationTuning ?? NavigationTuning.Default;
        var astar = new DeterministicAStar(tuning);
        var flags = tuning.AllowDiagonals ? PathFlags.AllowDiagonal : PathFlags.None;
        var req = new PathRequest(
            new Point3(_pathStart.Value.X, _pathStart.Value.Y, _pathStartZ),
            new Point3(cursorPosition.X, cursorPosition.Y, currentZ),
            MoveMode.Walk,
            flags,
            0);

        var path = astar.FindPath(req, navView);
        navigationOverlay.CurrentMode = NavigationOverlay.OverlayMode.PathDisplay;
        navigationOverlay.SetPath(path);

        double totalCost = path.TotalCost / 10.0;
        ui.AddToast($"Path: {path.Kind} len={path.Length} cost={totalCost:F1}", uiTick + 180);
    }

    private WorldNavigationView GetNavigationView(NavigationManager navigationManager, World world)
    {
        if (_navView == null || _viewNavigationManager != navigationManager || _viewWorld != world)
        {
            _navView = new WorldNavigationView(navigationManager);
            _viewNavigationManager = navigationManager;
            _viewWorld = world;
        }

        return _navView;
    }
}
