using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static class FortressZonesKeyboardInput
{
    public static bool Handle(
        Keyboard keyboard,
        UiStore ui,
        SimulationZoneCatalogData zoneCatalog,
        int currentZ,
        ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        if (ui.ZoneMenu == ZoneSubmenu.None)
            return HandleSubmenuSelection(keyboard, ui);

        foreach (var option in ZoneOptionPresentation.GetOptions(zoneCatalog, ui.ZoneMenu))
        {
            if (option.Keybind.Length != 1
                || !keyboard.IsKeyPressed((Keys)char.ToUpperInvariant(option.Keybind[0])))
                continue;

            return SelectZoneOption(ui, option, currentZ, uiTick);
        }

        if (keyboard.IsKeyPressed(Keys.OemComma))
        {
            ui.StartPlacement(PlacementMode.ZoneDelete, currentZ);
            ui.AddToast("Click zone to delete", uiTick + 150);
            return true;
        }

        return false;
    }

    internal static bool SelectZoneOption(
        UiStore ui,
        ZoneMenuOptionView option,
        int currentZ,
        ulong uiTick)
    {
        ui.SelectedZoneDefId = option.Id;
        ui.StartPlacement(PlacementMode.ZoneFirstCorner, currentZ);
        ui.AddToast($"Placing {option.DisplayName} - select first corner", uiTick + 150);
        return true;
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
