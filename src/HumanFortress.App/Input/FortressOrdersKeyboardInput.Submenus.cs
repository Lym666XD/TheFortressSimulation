using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressOrdersKeyboardInput
{
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
}
