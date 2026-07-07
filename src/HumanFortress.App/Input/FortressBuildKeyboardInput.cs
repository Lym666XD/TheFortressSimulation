using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressBuildKeyboardInput
{
    public static bool Handle(Keyboard keyboard, UiStore ui, int currentZ, ulong uiTick, SimulationBuildCatalogData buildCatalog)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        return ui.BuildMenu switch
        {
            BuildSubmenu.None => HandleSubmenuSelection(keyboard, ui),
            BuildSubmenu.Structural => HandleStructural(keyboard, ui, uiTick),
            BuildSubmenu.Workshop => HandleWorkshopMenu(keyboard, ui, currentZ, uiTick, buildCatalog),
            _ => HandlePlaceholderSubmenu(keyboard, ui, uiTick)
        };
    }

    private static bool HandleSubmenuSelection(Keyboard keyboard, UiStore ui)
    {
        if (keyboard.IsKeyPressed(Keys.Z)) { ui.OpenBuildSubmenu(BuildSubmenu.Structural); return true; }
        if (keyboard.IsKeyPressed(Keys.X)) { ui.OpenBuildSubmenu(BuildSubmenu.FunctionalStructure); return true; }
        if (keyboard.IsKeyPressed(Keys.C)) { ui.OpenBuildSubmenu(BuildSubmenu.Workshop); return true; }
        if (keyboard.IsKeyPressed(Keys.V)) { ui.OpenBuildSubmenu(BuildSubmenu.CivilFurniture); return true; }
        if (keyboard.IsKeyPressed(Keys.F)) { ui.OpenBuildSubmenu(BuildSubmenu.UtilityFurniture); return true; }
        return false;
    }

    private static bool HandlePlaceholderSubmenu(Keyboard keyboard, UiStore ui, ulong uiTick)
    {
        if (!keyboard.IsKeyPressed(Keys.OemComma))
            return false;

        ui.AddToast("Back", uiTick + 60);
        return true;
    }
}
