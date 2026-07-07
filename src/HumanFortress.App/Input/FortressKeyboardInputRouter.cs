using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressKeyboardInputRouter
{
    public static FortressKeyboardInputResult Process(FortressKeyboardInputRouterContext context, Keyboard keyboard)
    {
        ArgumentNullException.ThrowIfNull(context.Runtime);
        ArgumentNullException.ThrowIfNull(context.Ui);
        ArgumentNullException.ThrowIfNull(context.NavigationDebug);
        ArgumentNullException.ThrowIfNull(context.HideTilePanel);
        ArgumentNullException.ThrowIfNull(context.CreateStockpile);
        ArgumentNullException.ThrowIfNull(keyboard);

        if (FortressWorkshopPanelKeyboardInput.Handle(keyboard, context.Runtime.WorkshopPanel, context.Ui, context.UiTick))
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
        changed |= FortressSimulationKeyboardInput.Handle(keyboard, context.Runtime.SimulationControl, context.Ui, context.UiTick);
        changed |= FortressGlobalUiKeyboardInput.HandleHelpAndDebug(keyboard, context.Ui);

        if (FortressGlobalUiKeyboardInput.TryHandleDrawerShortcut(keyboard, context.Ui, context.HideTilePanel))
        {
            changed = true;
        }
        else
        {
            changed |= context.NavigationDebug.HandleKeyboard(
                keyboard,
                context.Runtime.NavigationDebug,
                context.NavigationOverlay,
                context.Viewport.CursorPosition,
                currentZ,
                context.Ui,
                context.UiTick);
        }

        changed |= FortressContextKeyboardInput.Handle(new FortressContextKeyboardInputContext(
            keyboard,
            context.Ui,
            context.UiServices?.ZonesUI,
            context.UiServices?.StockpileUI,
            currentZ,
            context.UiTick,
            context.Runtime.BuildCatalog.GetBuildCatalogData(),
            context.TileInspectionOpen,
            context.HideTilePanel,
            context.CreateStockpile));

        return new FortressKeyboardInputResult(true, changed, cameraPosition, currentZ);
    }
}
