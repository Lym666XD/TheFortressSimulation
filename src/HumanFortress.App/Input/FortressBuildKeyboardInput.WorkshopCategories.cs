using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressBuildKeyboardInput
{
    private static bool HandleWorkshopCategorySelection(Keyboard keyboard, UiStore ui, ulong uiTick, SimulationBuildCatalogData buildCatalog)
    {
        var categories = WorkshopCategoryPresentation.GetCategories(buildCatalog);
        if (keyboard.IsKeyPressed(Keys.Z)) return SelectWorkshopCategory(ui, uiTick, categories, 0);
        if (keyboard.IsKeyPressed(Keys.X)) return SelectWorkshopCategory(ui, uiTick, categories, 1);
        if (keyboard.IsKeyPressed(Keys.C)) return SelectWorkshopCategory(ui, uiTick, categories, 2);
        if (keyboard.IsKeyPressed(Keys.V)) return SelectWorkshopCategory(ui, uiTick, categories, 3);
        if (keyboard.IsKeyPressed(Keys.F)) return SelectWorkshopCategory(ui, uiTick, categories, 4);
        if (keyboard.IsKeyPressed(Keys.OemComma)) { ui.CloseBuildSubmenu(); return true; }
        return false;
    }

    internal static bool SelectWorkshopCategory(
        UiStore ui,
        ulong uiTick,
        IReadOnlyList<WorkshopCategoryView> categories,
        int index)
    {
        if (index < 0 || index >= categories.Count)
        {
            ui.AddToast("[WORKSHOP] WIP", uiTick + 100);
            return true;
        }

        var category = categories[index];
        ui.SelectedWorkshopCategory = category.Id;
        if (category.Workshops == null || category.Workshops.Count == 0)
        {
            ui.AddToast("[WORKSHOP] WIP", uiTick + 100);
            return true;
        }

        ui.WorkshopBrowsingItems = true;
        ui.AddToast(category.DisplayName, uiTick + 100);
        return true;
    }
}
