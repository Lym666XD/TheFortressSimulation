using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static class FortressZonesKeyboardInput
{
    private static readonly char[] ZoneKeys = { 'z', 'x', 'c', 'v', 'f', 'g', 'r', 't' };

    public static bool Handle(Keyboard keyboard, UiStore ui, ZonesUI? zonesUI, int currentZ, ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        if (ui.ZoneMenu == ZoneSubmenu.None)
            return HandleSubmenuSelection(keyboard, ui);

        foreach (var c in ZoneKeys)
        {
            if (!keyboard.IsKeyPressed((Keys)char.ToUpperInvariant(c)))
                continue;

            var defId = zonesUI?.GetZoneDefIdFromKey(ui.ZoneMenu, c);
            if (defId != null)
            {
                ui.SelectedZoneDefId = defId;
                ui.StartPlacement(PlacementMode.ZoneFirstCorner, currentZ);
                ui.AddToast($"Placing {defId} zone - select first corner", uiTick + 150);
                return true;
            }

            break;
        }

        if (keyboard.IsKeyPressed(Keys.OemComma))
        {
            ui.StartPlacement(PlacementMode.ZoneDelete, currentZ);
            ui.AddToast("Click zone to delete", uiTick + 150);
            return true;
        }

        return false;
    }

    private static bool HandleSubmenuSelection(Keyboard keyboard, UiStore ui)
    {
        if (keyboard.IsKeyPressed(Keys.Z)) { ui.OpenZoneSubmenu(ZoneSubmenu.Production); return true; }
        if (keyboard.IsKeyPressed(Keys.X)) { ui.OpenZoneSubmenu(ZoneSubmenu.Civil); return true; }
        if (keyboard.IsKeyPressed(Keys.C)) { ui.OpenZoneSubmenu(ZoneSubmenu.Public); return true; }
        if (keyboard.IsKeyPressed(Keys.V)) { ui.OpenZoneSubmenu(ZoneSubmenu.Military); return true; }
        if (keyboard.IsKeyPressed(Keys.F)) { ui.OpenZoneSubmenu(ZoneSubmenu.Management); return true; }
        return false;
    }
}
