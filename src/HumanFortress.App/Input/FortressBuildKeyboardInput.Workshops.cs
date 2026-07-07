using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressBuildKeyboardInput
{
    private static bool HandleWorkshopMenu(Keyboard keyboard, UiStore ui, int currentZ, ulong uiTick, SimulationBuildCatalogData buildCatalog)
    {
        if (!ui.WorkshopBrowsingItems)
            return HandleWorkshopCategorySelection(keyboard, ui, uiTick, buildCatalog);

        int pick = GetWorkshopPick(keyboard);
        if (pick >= 0)
        {
            return SelectWorkshopByIndex(ui, currentZ, uiTick, buildCatalog, pick);
        }

        if (keyboard.IsKeyPressed(Keys.OemComma))
        {
            ui.WorkshopBrowsingItems = false;
            ui.SelectedWorkshopCategory = null;
            ui.AddToast("Back", uiTick + 60);
            return true;
        }

        return false;
    }
}
