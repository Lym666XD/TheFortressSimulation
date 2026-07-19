using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressBuildKeyboardInput
{
    private static bool HandleStructural(
        Keyboard keyboard,
        UiStore ui,
        ulong uiTick,
        SimulationBuildCatalogData buildCatalog)
    {
        if (keyboard.IsKeyPressed(Keys.Z))
            return OpenMaterialDialog(ui, uiTick, UiConstructionShape.Wall, buildCatalog);

        if (keyboard.IsKeyPressed(Keys.X))
            return OpenMaterialDialog(ui, uiTick, UiConstructionShape.Floor, buildCatalog);

        if (keyboard.IsKeyPressed(Keys.C))
            return OpenMaterialDialog(ui, uiTick, UiConstructionShape.Ramp, buildCatalog);

        if (keyboard.IsKeyPressed(Keys.V))
        {
            ui.AddToast("Stairs (multi-level) WIP", uiTick + 150);
            return true;
        }

        return false;
    }

    private static bool OpenMaterialDialog(
        UiStore ui,
        ulong uiTick,
        UiConstructionShape shape,
        SimulationBuildCatalogData buildCatalog)
    {
        ui.SelectedConstructionShape = shape;
        ui.ResetConstructionSelection();
        var options = ConstructionMaterialOptionPresentation.GetOptions(buildCatalog, shape);
        if (options.Count == 0)
        {
            ui.ConstructionMaterialDialogOpen = false;
            ui.AddToast($"No {shape} materials available", uiTick + 120);
            return true;
        }

        ui.ConstructionMaterialDialogOpen = true;
        Logger.Log($"[BUILD.UI] Open material dialog shape={shape}");
        ui.AddToast($"Choose {shape} material", uiTick + 200);
        return true;
    }
}
