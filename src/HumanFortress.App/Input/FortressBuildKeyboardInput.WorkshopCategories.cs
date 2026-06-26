using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressBuildKeyboardInput
{
    private static bool HandleWorkshopCategorySelection(Keyboard keyboard, UiStore ui, ulong uiTick, SimulationBuildCatalogData buildCatalog)
    {
        if (keyboard.IsKeyPressed(Keys.Z)) return SelectWorkshopCategory(ui, uiTick, buildCatalog, "mining");
        if (keyboard.IsKeyPressed(Keys.X)) return SelectWorkshopCategory(ui, uiTick, buildCatalog, "industry");
        if (keyboard.IsKeyPressed(Keys.C)) return SelectWorkshopCategory(ui, uiTick, buildCatalog, "farming");
        if (keyboard.IsKeyPressed(Keys.V)) return SelectWorkshopCategory(ui, uiTick, buildCatalog, "lumbering");
        if (keyboard.IsKeyPressed(Keys.F)) return SelectWorkshopCategory(ui, uiTick, buildCatalog, "crafts");
        if (keyboard.IsKeyPressed(Keys.OemComma)) { ui.CloseBuildSubmenu(); return true; }
        return false;
    }

    private static bool SelectWorkshopCategory(UiStore ui, ulong uiTick, SimulationBuildCatalogData buildCatalog, string category)
    {
        ui.SelectedWorkshopCategory = category;
        var list = WorkshopCategoryMapper.GetWorkshopsByCategory(buildCatalog, category);
        if (list.Count == 0)
        {
            ui.AddToast("[WORKSHOP] WIP", uiTick + 100);
            return true;
        }

        ui.WorkshopBrowsingItems = true;
        ui.AddToast(GetWorkshopCategoryDisplayName(category), uiTick + 100);
        return true;
    }

    private static string GetWorkshopCategoryDisplayName(string category)
    {
        return category switch
        {
            "mining" => "Mining",
            "industry" => "Industry",
            "farming" => "Farming",
            "lumbering" => "Lumbering",
            "crafts" => "Crafts",
            _ => category
        };
    }
}
