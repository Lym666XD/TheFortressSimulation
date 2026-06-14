using HumanFortress.App.UI;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.Orders;
using SadConsole.Input;

namespace HumanFortress.App.Runtime;

internal static class FortressBuildKeyboardInput
{
    private static readonly Keys[] WorkshopChoiceKeys =
    {
        Keys.Z,
        Keys.X,
        Keys.C,
        Keys.V,
        Keys.F,
        Keys.G,
        Keys.R,
        Keys.T
    };

    public static bool Handle(Keyboard keyboard, UiStore ui, int currentZ, ulong uiTick, IConstructionCatalog? constructions)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        return ui.BuildMenu switch
        {
            BuildSubmenu.None => HandleSubmenuSelection(keyboard, ui),
            BuildSubmenu.Structural => HandleStructural(keyboard, ui, uiTick),
            BuildSubmenu.Workshop => HandleWorkshopMenu(keyboard, ui, currentZ, uiTick, constructions),
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

    private static bool HandleStructural(Keyboard keyboard, UiStore ui, ulong uiTick)
    {
        if (keyboard.IsKeyPressed(Keys.Z))
            return OpenMaterialDialog(ui, uiTick, ConstructionShape.Wall, "Wall: choose material [Z]=Stone [X]=Log");

        if (keyboard.IsKeyPressed(Keys.X))
            return OpenMaterialDialog(ui, uiTick, ConstructionShape.Floor, "Floor: [Z]=Stone [X]=Plank");

        if (keyboard.IsKeyPressed(Keys.C))
            return OpenMaterialDialog(ui, uiTick, ConstructionShape.Ramp, "Ramp: [ENTER]=Stone+Plank");

        if (keyboard.IsKeyPressed(Keys.V))
        {
            ui.AddToast("Stairs (multi-level) WIP", uiTick + 150);
            return true;
        }

        return false;
    }

    private static bool OpenMaterialDialog(UiStore ui, ulong uiTick, ConstructionShape shape, string toast)
    {
        ui.SelectedConstructionShape = shape;
        ui.ResetConstructionSelection();
        ui.ConstructionMaterialDialogOpen = true;
        Logger.Log($"[BUILD.UI] Open material dialog shape={shape}");
        ui.AddToast(toast, uiTick + 200);
        return true;
    }

    private static bool HandleWorkshopMenu(Keyboard keyboard, UiStore ui, int currentZ, ulong uiTick, IConstructionCatalog? constructions)
    {
        if (!ui.WorkshopBrowsingItems)
            return HandleWorkshopCategorySelection(keyboard, ui, uiTick, constructions);

        int pick = -1;
        for (int i = 0; i < WorkshopChoiceKeys.Length; i++)
        {
            if (keyboard.IsKeyPressed(WorkshopChoiceKeys[i]))
            {
                pick = i;
                break;
            }
        }

        if (pick >= 0)
        {
            var id = GetWorkshopIdByCategoryIndex(constructions, ui.SelectedWorkshopCategory, pick);
            if (id == null)
            {
                ui.AddToast("[WORKSHOP] WIP", uiTick + 100);
            }
            else
            {
                ui.SelectedBuildableConstructionId = id;
                ui.StartPlacement(PlacementMode.BuildableFirstAnchor, currentZ);
                Logger.Log($"[BUILD.UI] Workshop select id={id}");
                ui.AddToast("Workshop selected", uiTick + 120);
            }

            return true;
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

    private static bool HandleWorkshopCategorySelection(Keyboard keyboard, UiStore ui, ulong uiTick, IConstructionCatalog? constructions)
    {
        if (keyboard.IsKeyPressed(Keys.Z)) return SelectWorkshopCategory(ui, uiTick, constructions, "mining");
        if (keyboard.IsKeyPressed(Keys.X)) return SelectWorkshopCategory(ui, uiTick, constructions, "industry");
        if (keyboard.IsKeyPressed(Keys.C)) return SelectWorkshopCategory(ui, uiTick, constructions, "farming");
        if (keyboard.IsKeyPressed(Keys.V)) return SelectWorkshopCategory(ui, uiTick, constructions, "lumbering");
        if (keyboard.IsKeyPressed(Keys.F)) return SelectWorkshopCategory(ui, uiTick, constructions, "crafts");
        if (keyboard.IsKeyPressed(Keys.OemComma)) { ui.CloseBuildSubmenu(); return true; }
        return false;
    }

    private static bool SelectWorkshopCategory(UiStore ui, ulong uiTick, IConstructionCatalog? constructions, string category)
    {
        ui.SelectedWorkshopCategory = category;
        var list = WorkshopCategoryMapper.GetWorkshopsByCategory(constructions, category);
        if (list.Count == 0)
        {
            ui.AddToast("[WORKSHOP] WIP", uiTick + 100);
            return true;
        }

        ui.WorkshopBrowsingItems = true;
        ui.AddToast(GetWorkshopCategoryDisplayName(category), uiTick + 100);
        return true;
    }

    private static string? GetWorkshopIdByCategoryIndex(IConstructionCatalog? constructions, string? category, int index)
    {
        if (string.IsNullOrWhiteSpace(category))
            return null;

        var list = WorkshopCategoryMapper.GetWorkshopsByCategory(constructions, category);
        if (index < 0 || index >= list.Count)
            return null;

        return list[index].Id;
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

    private static bool HandlePlaceholderSubmenu(Keyboard keyboard, UiStore ui, ulong uiTick)
    {
        if (!keyboard.IsKeyPressed(Keys.OemComma))
            return false;

        ui.AddToast("Back", uiTick + 60);
        return true;
    }
}
