using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressOrdersKeyboardInput
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

}
