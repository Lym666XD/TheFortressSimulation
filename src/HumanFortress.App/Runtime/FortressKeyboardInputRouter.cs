using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using HumanFortress.Navigation;
using HumanFortress.Simulation.World;
using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal readonly record struct FortressKeyboardInputRouterContext(
    FortressRuntimeAccess Runtime,
    UiStore Ui,
    ulong UiTick,
    FortressViewportSnapshot Viewport,
    FortressLoadedSessionSnapshot LoadedSession,
    ISelectionTool? SelectionTool,
    FortressNavigationDebugController NavigationDebug,
    bool TileInspectionOpen,
    Action HideTilePanel,
    Func<Guid, FortressWorkshopPanelContext?> FindWorkshop,
    Action<string> CreateStockpile);

internal readonly record struct FortressKeyboardInputResult(
    bool Handled,
    bool ShouldRedraw,
    Point CameraPosition,
    int CurrentZ);

internal static class FortressKeyboardInputRouter
{
    public static FortressKeyboardInputResult Process(FortressKeyboardInputRouterContext context, Keyboard keyboard)
    {
        ArgumentNullException.ThrowIfNull(context.Runtime);
        ArgumentNullException.ThrowIfNull(context.Ui);
        ArgumentNullException.ThrowIfNull(context.NavigationDebug);
        ArgumentNullException.ThrowIfNull(context.HideTilePanel);
        ArgumentNullException.ThrowIfNull(context.FindWorkshop);
        ArgumentNullException.ThrowIfNull(context.CreateStockpile);
        ArgumentNullException.ThrowIfNull(keyboard);

        if (FortressWorkshopPanelKeyboardInput.Handle(keyboard, context.Runtime, context.Ui, context.UiTick, context.FindWorkshop))
            return Redraw(context);

        if (context.Ui.ConstructionMaterialDialogOpen)
        {
            bool dialogChanged = FortressConstructionMaterialDialogInput.Handle(keyboard, context.Ui, context.Viewport.CurrentZ, context.UiTick);
            return new FortressKeyboardInputResult(true, dialogChanged, context.Viewport.CameraPosition, context.Viewport.CurrentZ);
        }

        bool changed = false;
        var cameraPosition = context.Viewport.CameraPosition;
        var currentZ = context.Viewport.CurrentZ;

        changed |= ApplyNavigation(context, keyboard, ref cameraPosition, ref currentZ);
        changed |= FortressSimulationKeyboardInput.Handle(keyboard, context.Runtime, context.Ui, context.UiTick);
        changed |= FortressGlobalUiKeyboardInput.HandleHelpAndDebug(keyboard, context.Ui);

        if (FortressGlobalUiKeyboardInput.TryHandleDrawerShortcut(keyboard, context.Ui, context.HideTilePanel))
        {
            changed = true;
        }
        else
        {
            changed |= context.NavigationDebug.HandleKeyboard(
                keyboard,
                context.LoadedSession.NavigationOverlay,
                context.LoadedSession.NavigationManager,
                context.Runtime.NavigationTuning,
                context.LoadedSession.World,
                context.Viewport.CursorPosition,
                currentZ,
                context.Ui,
                context.UiTick);
        }

        changed |= FortressContextKeyboardInput.Handle(new FortressContextKeyboardInputContext(
            keyboard,
            context.Ui,
            context.LoadedSession.UiServices?.ZonesUI,
            context.LoadedSession.UiServices?.StockpileUI,
            currentZ,
            context.UiTick,
            context.Runtime.Constructions,
            context.TileInspectionOpen,
            context.HideTilePanel,
            context.CreateStockpile));

        return new FortressKeyboardInputResult(true, changed, cameraPosition, currentZ);
    }

    private static FortressKeyboardInputResult Redraw(FortressKeyboardInputRouterContext context)
    {
        return new FortressKeyboardInputResult(true, true, context.Viewport.CameraPosition, context.Viewport.CurrentZ);
    }

    private static bool ApplyNavigation(
        FortressKeyboardInputRouterContext context,
        Keyboard keyboard,
        ref Point cameraPosition,
        ref int currentZ)
    {
        int previousZ = currentZ;
        var navigationInput = FortressKeyboardNavigationInput.Handle(keyboard, cameraPosition, currentZ, context.SelectionTool);
        cameraPosition = navigationInput.CameraPosition;
        currentZ = navigationInput.CurrentZ;

        if (currentZ != previousZ)
            FortressMiningZRangeSync.ApplyCurrentZ(context.Ui, context.SelectionTool, currentZ);

        return navigationInput.Changed;
    }
}
