using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static class FortressConstructionMaterialDialogInput
{
    public static bool Handle(
        Keyboard keyboard,
        UiStore ui,
        int currentZ,
        ulong uiTick,
        SimulationBuildCatalogData buildCatalog)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        if (!ui.ConstructionMaterialDialogOpen)
            return false;

        if (keyboard.IsKeyPressed(Keys.Escape))
        {
            ui.ConstructionMaterialDialogOpen = false;
            return true;
        }

        var options = ConstructionMaterialOptionPresentation.GetOptions(
            buildCatalog,
            ui.SelectedConstructionShape);
        int selectedIndex = GetSelectedOptionIndex(keyboard, options.Count);
        if (selectedIndex >= 0)
            return ApplySelection(ui, currentZ, uiTick, options[selectedIndex]);

        return false;
    }

    internal static bool ApplySelection(
        UiStore ui,
        int currentZ,
        ulong uiTick,
        ConstructionMaterialOptionView option)
    {
        ArgumentNullException.ThrowIfNull(ui);

        ui.ConstructionMaterialRequirements.Clear();
        foreach (var requirement in option.Requirements)
            ui.ConstructionMaterialRequirements.Add(requirement);

        ui.ConstructionResultMaterialId = option.ResultMaterialId;
        Logger.Log($"[BUILD.UI] Selected construction option={option.Id}");
        ui.ConstructionMaterialDialogOpen = false;
        ui.StartPlacement(PlacementMode.ConstructionFirstCorner, currentZ);
        ui.AddToast(option.Name, uiTick + 100);
        return true;
    }

    private static int GetSelectedOptionIndex(Keyboard keyboard, int optionCount)
    {
        if (optionCount == 1 && keyboard.IsKeyPressed(Keys.Enter))
            return 0;
        if (optionCount > 0 && keyboard.IsKeyPressed(Keys.Z))
            return 0;
        if (optionCount > 1 && keyboard.IsKeyPressed(Keys.X))
            return 1;
        if (optionCount > 2 && keyboard.IsKeyPressed(Keys.C))
            return 2;
        if (optionCount > 3 && keyboard.IsKeyPressed(Keys.V))
            return 3;
        if (optionCount > 4 && keyboard.IsKeyPressed(Keys.F))
            return 4;

        return -1;
    }
}
