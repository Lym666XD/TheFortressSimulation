using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressBuildKeyboardInput
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

    private static int GetWorkshopPick(Keyboard keyboard)
    {
        for (int i = 0; i < WorkshopChoiceKeys.Length; i++)
        {
            if (keyboard.IsKeyPressed(WorkshopChoiceKeys[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool SelectWorkshopByIndex(
        UiStore ui,
        int currentZ,
        ulong uiTick,
        SimulationBuildCatalogData buildCatalog,
        int index)
    {
        var id = GetWorkshopIdByCategoryIndex(buildCatalog, ui.SelectedWorkshopCategory, index);
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

    private static string? GetWorkshopIdByCategoryIndex(SimulationBuildCatalogData buildCatalog, string? category, int index)
    {
        if (string.IsNullOrWhiteSpace(category))
            return null;

        var list = WorkshopCategoryMapper.GetWorkshopsByCategory(buildCatalog, category);
        if (index < 0 || index >= list.Count)
            return null;

        return list[index].Id;
    }
}
