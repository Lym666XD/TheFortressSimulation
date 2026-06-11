using HumanFortress.App.UI;
using HumanFortress.App.UI.Components;
using HumanFortress.App.UI.Selection;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal static class FortressInteractionBootstrapper
{
    public static FortressInteractionSetup Configure(
        FortressScreenLayout layout,
        FortressRuntimeAccess runtime,
        UiStore ui,
        int worldSizeTiles,
        Func<ulong> uiTickProvider,
        Func<World?> worldProvider,
        Action<Point> onMapMouseMoved,
        Action<Point> onMapLeftClicked,
        Action<Point> onOverlayLeftClicked,
        Action<Point> onOverlayRightClicked,
        Action<Point> onOverlayMouseMoved,
        Action redraw)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(uiTickProvider);
        ArgumentNullException.ThrowIfNull(worldProvider);
        ArgumentNullException.ThrowIfNull(onMapMouseMoved);
        ArgumentNullException.ThrowIfNull(onMapLeftClicked);
        ArgumentNullException.ThrowIfNull(onOverlayLeftClicked);
        ArgumentNullException.ThrowIfNull(onOverlayRightClicked);
        ArgumentNullException.ThrowIfNull(onOverlayMouseMoved);
        ArgumentNullException.ThrowIfNull(redraw);

        layout.MapSurface.MouseMovedLocal += onMapMouseMoved;
        layout.MapSurface.LeftClickedLocal += onMapLeftClicked;

        var selectionTool = new DragRectSelectionTool(worldSizeTiles);
        selectionTool.Started += s => { ui.PlaceZMin = s.ZMin; ui.PlaceZMax = s.ZMax; };
        selectionTool.Changed += s => { ui.PlaceZMin = s.ZMin; ui.PlaceZMax = s.ZMax; redraw(); };
        selectionTool.Completed += s => { ui.PlaceZMin = s.ZMin; ui.PlaceZMax = s.ZMax; };
        selectionTool.Canceled += redraw;

        var uiStateManager = new UIStateManager(ui);
        var inputHandler = new InputHandlerComponent(
            uiStateManager,
            layout.UiSurface.Surface.Width,
            layout.UiSurface.Surface.Height,
            uiTickProvider,
            worldProvider,
            () => runtime.ProfessionAssignments,
            () => runtime.GetProfessionRosterSnapshot(),
            runtime.SetProfessionWeight
        );
        layout.UiSurface.SadComponents.Add(inputHandler);
        Logger.Log("[INIT] Added InputHandlerComponent to UiOverlay");

        layout.UiSurface.LeftClickedLocal += onOverlayLeftClicked;
        layout.UiSurface.RightClickedLocal += onOverlayRightClicked;
        layout.UiSurface.MouseMovedLocal += onOverlayMouseMoved;
        Logger.Log($"[INIT] UiOverlay size={layout.UiSurface.Surface.Width}x{layout.UiSurface.Surface.Height}");

        return new FortressInteractionSetup(selectionTool);
    }
}
