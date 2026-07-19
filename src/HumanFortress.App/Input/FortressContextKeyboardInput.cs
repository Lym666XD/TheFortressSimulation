using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal readonly record struct FortressContextKeyboardInputContext(
    Keyboard Keyboard,
    UiStore Ui,
    StockpileUI? StockpileUi,
    int CurrentZ,
    ulong UiTick,
    SimulationBuildCatalogData BuildCatalog,
    SimulationZoneCatalogData ZoneCatalog,
    SimulationDebugMenuData DebugMenu,
    bool TilePanelOpen,
    Action HideTilePanel,
    Action<string> CreateStockpile);

internal static class FortressContextKeyboardInput
{
    public static bool Handle(FortressContextKeyboardInputContext context)
    {
        ArgumentNullException.ThrowIfNull(context.Keyboard);
        ArgumentNullException.ThrowIfNull(context.Ui);
        ArgumentNullException.ThrowIfNull(context.HideTilePanel);
        ArgumentNullException.ThrowIfNull(context.CreateStockpile);

        var keyboard = context.Keyboard;
        var ui = context.Ui;
        bool changed = false;

        if (ui.DebugOpen)
        {
            DebugSelectionPolicy.EnsureValidSelections(ui, context.DebugMenu);
            changed |= FortressDebugMenuInput.Handle(keyboard, ui, context.DebugMenu);
        }
        else if (ui.Context == UiContext.QuickMenu && ui.QuickMenu == QuickMenuKind.Orders)
        {
            changed |= FortressOrdersKeyboardInput.Handle(keyboard, ui, context.CurrentZ, context.UiTick);
        }
        else if (ui.Context == UiContext.QuickMenu && ui.QuickMenu == QuickMenuKind.Zones)
        {
            changed |= FortressZonesKeyboardInput.Handle(
                keyboard,
                ui,
                context.ZoneCatalog,
                context.CurrentZ,
                context.UiTick);
        }
        else if (ui.Context == UiContext.QuickMenu && ui.QuickMenu == QuickMenuKind.Build)
        {
            changed |= FortressBuildKeyboardInput.Handle(keyboard, ui, context.CurrentZ, context.UiTick, context.BuildCatalog);
        }
        else if (ui.Context == UiContext.QuickMenu && ui.QuickMenu == QuickMenuKind.Stockpile)
        {
            changed |= FortressStockpileKeyboardInput.Handle(keyboard, ui, context.CurrentZ, context.UiTick);
        }
        else if (ui.Context == UiContext.PlacingTool)
        {
            changed |= FortressStockpilePresetKeyboardInput.Handle(
                keyboard,
                ui,
                context.StockpileUi,
                context.CreateStockpile);
        }
        else if (ui.Context == UiContext.Global)
        {
            changed |= FortressGlobalUiKeyboardInput.HandleGlobalQuickMenus(keyboard, ui, context.HideTilePanel);
        }

        changed |= FortressGlobalUiKeyboardInput.HandleDrawerTabs(keyboard, ui);
        changed |= FortressEscapeKeyboardInput.Handle(
            keyboard,
            ui,
            context.TilePanelOpen,
            context.StockpileUi,
            context.HideTilePanel);

        return changed;
    }
}
