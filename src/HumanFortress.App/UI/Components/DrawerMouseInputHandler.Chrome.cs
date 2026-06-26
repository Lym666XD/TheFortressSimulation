using HumanFortress.App.UI.Commands;
using SadRogue.Primitives;

namespace HumanFortress.App.UI.Components;

internal sealed partial class DrawerMouseInputHandler
{
    private bool TryHandleDockClick(Point localPos)
    {
        int? dockSlot = ButtonLayoutCalculator.HitTestDockButtons(localPos, _screenWidth, _screenHeight);
        if (!dockSlot.HasValue || !UiChromeSlots.TryGetDockSlot(dockSlot.Value, out var dock))
        {
            return false;
        }

        new ToggleDrawerCommand(dock.Drawer).Execute(_uiStateManager);
        Logger.Log($"[InputHandler] Dock button {dockSlot.Value} -> {dock.Drawer}");
        _uiStateManager.Store.SuppressNextTileClick = true;
        return true;
    }

    private bool TryHandleQuickClick(Point localPos)
    {
        int? quickSlot = ButtonLayoutCalculator.HitTestQuickButtons(localPos, _screenWidth, _screenHeight);
        if (!quickSlot.HasValue || !UiChromeSlots.TryGetQuickSlot(quickSlot.Value, out var quick))
        {
            return false;
        }

        new ToggleQuickMenuCommand(quick.Menu).Execute(_uiStateManager);
        Logger.Log($"[InputHandler] Quick button {quickSlot.Value} -> {quick.Menu}");
        _uiStateManager.Store.SuppressNextTileClick = true;
        return true;
    }
}
