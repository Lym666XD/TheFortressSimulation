using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Runtime;

internal static class FortressOrdersKeyboardInput
{
    public static bool Handle(Keyboard keyboard, UiStore ui, int currentZ, ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        if (ui.OrdersMenu == OrdersSubmenu.None)
            return HandleSubmenuSelection(keyboard, ui);

        return ui.OrdersMenu switch
        {
            OrdersSubmenu.Mining => HandleMining(keyboard, ui, currentZ, uiTick),
            OrdersSubmenu.Lumbering => HandleWipSubmenu(keyboard, ui, uiTick, ("Lumber: WIP", Keys.Z)),
            OrdersSubmenu.Gather => HandleWipSubmenu(keyboard, ui, uiTick, ("Gather plant: WIP", Keys.Z), ("Remove plant: WIP", Keys.X)),
            OrdersSubmenu.Masonry => HandleWipSubmenu(keyboard, ui, uiTick,
                ("Smooth: WIP", Keys.Z),
                ("Engrave: WIP", Keys.X),
                ("Track: WIP", Keys.C),
                ("Carve gap: WIP", Keys.V)),
            OrdersSubmenu.Haul => HandleHaul(keyboard, ui, currentZ, uiTick),
            OrdersSubmenu.Creature => HandleWipSubmenu(keyboard, ui, uiTick,
                ("Hunting: WIP", Keys.Z),
                ("Kill: WIP", Keys.X),
                ("Tame: WIP", Keys.C),
                ("Rescue: WIP", Keys.V)),
            OrdersSubmenu.Other => HandleWipSubmenu(keyboard, ui, uiTick,
                ("Lock/disallow: WIP", Keys.Z),
                ("Unlock/allow: WIP", Keys.X),
                ("Dump: WIP", Keys.C),
                ("Remove dump: WIP", Keys.V),
                ("Melt: WIP", Keys.F),
                ("Remove melt: WIP", Keys.T),
                ("Clean: WIP", Keys.R)),
            _ => false
        };
    }

    private static bool HandleSubmenuSelection(Keyboard keyboard, UiStore ui)
    {
        if (keyboard.IsKeyPressed(Keys.Z)) { ui.OpenOrdersSubmenu(OrdersSubmenu.Mining); return true; }
        if (keyboard.IsKeyPressed(Keys.X)) { ui.OpenOrdersSubmenu(OrdersSubmenu.Lumbering); return true; }
        if (keyboard.IsKeyPressed(Keys.C)) { ui.OpenOrdersSubmenu(OrdersSubmenu.Gather); return true; }
        if (keyboard.IsKeyPressed(Keys.V)) { ui.OpenOrdersSubmenu(OrdersSubmenu.Masonry); return true; }
        if (keyboard.IsKeyPressed(Keys.F)) { ui.OpenOrdersSubmenu(OrdersSubmenu.Haul); return true; }
        if (keyboard.IsKeyPressed(Keys.B)) { ui.OpenOrdersSubmenu(OrdersSubmenu.Creature); return true; }
        if (keyboard.IsKeyPressed(Keys.G)) { ui.OpenOrdersSubmenu(OrdersSubmenu.Other); return true; }
        return false;
    }

    private static bool HandleMining(Keyboard keyboard, UiStore ui, int currentZ, ulong uiTick)
    {
        if (keyboard.IsKeyPressed(Keys.Z))
            return StartMining(ui, currentZ, uiTick, MiningAction.Dig, "Mining: Dig - select first corner");
        if (keyboard.IsKeyPressed(Keys.X))
            return StartMining(ui, currentZ, uiTick, MiningAction.DigStairwell, "Mining: Stairwell - select first corner");
        if (keyboard.IsKeyPressed(Keys.C))
            return StartMining(ui, currentZ, uiTick, MiningAction.DigRamp, "Mining: Ramp - select first corner");
        if (keyboard.IsKeyPressed(Keys.V))
            return StartMining(ui, currentZ, uiTick, MiningAction.DigChannel, "Mining: Channel - select first corner");
        if (keyboard.IsKeyPressed(Keys.F))
            return StartMining(ui, currentZ, uiTick, MiningAction.RemoveDigging, "Mining: Remove designations - select first corner");
        if (keyboard.IsKeyPressed(Keys.OemComma)) { ui.CancelPlacement(); return true; }
        return false;
    }

    private static bool StartMining(UiStore ui, int currentZ, ulong uiTick, MiningAction action, string toast)
    {
        ui.SelectedMiningAction = action;
        ui.StartPlacement(PlacementMode.MiningFirstCorner, currentZ);
        ui.AddToast(toast, uiTick + 120);
        return true;
    }

    private static bool HandleHaul(Keyboard keyboard, UiStore ui, int currentZ, ulong uiTick)
    {
        if (keyboard.IsKeyPressed(Keys.Z))
        {
            ui.StartPlacement(PlacementMode.HaulFirstCorner, currentZ);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.X))
        {
            ui.AddToast("Emergency haul: WIP", uiTick + 120);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.OemComma))
        {
            ui.CancelPlacement();
            return true;
        }

        return false;
    }

    private static bool HandleWipSubmenu(Keyboard keyboard, UiStore ui, ulong uiTick, params (string Toast, Keys Key)[] actions)
    {
        foreach (var (toast, key) in actions)
        {
            if (keyboard.IsKeyPressed(key))
            {
                ui.AddToast(toast, uiTick + 120);
                return true;
            }
        }

        if (keyboard.IsKeyPressed(Keys.OemComma))
        {
            ui.CancelPlacement();
            return true;
        }

        return false;
    }
}
