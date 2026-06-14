using HumanFortress.App.UI;
using HumanFortress.Core.Content.Registry;
using SadConsole.Input;

namespace HumanFortress.App.Runtime;

internal readonly record struct FortressContextKeyboardInputContext(
    Keyboard Keyboard,
    UiStore Ui,
    ZonesUI? ZonesUi,
    StockpileUI? StockpileUi,
    int CurrentZ,
    ulong UiTick,
    IConstructionCatalog? Constructions,
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
            changed |= FortressDebugMenuInput.Handle(keyboard, ui);
        }
        else if (ui.Context == UiContext.QuickMenu && ui.QuickMenu == QuickMenuKind.Orders)
        {
            changed |= FortressOrdersKeyboardInput.Handle(keyboard, ui, context.CurrentZ, context.UiTick);
        }
        else if (ui.Context == UiContext.QuickMenu && ui.QuickMenu == QuickMenuKind.Zones)
        {
            changed |= FortressZonesKeyboardInput.Handle(keyboard, ui, context.ZonesUi, context.CurrentZ, context.UiTick);
        }
        else if (ui.Context == UiContext.QuickMenu && ui.QuickMenu == QuickMenuKind.Build)
        {
            changed |= FortressBuildKeyboardInput.Handle(keyboard, ui, context.CurrentZ, context.UiTick, context.Constructions);
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
