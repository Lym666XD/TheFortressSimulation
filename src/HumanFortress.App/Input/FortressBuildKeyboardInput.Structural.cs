using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressBuildKeyboardInput
{
    private static bool HandleStructural(Keyboard keyboard, UiStore ui, ulong uiTick)
    {
        if (keyboard.IsKeyPressed(Keys.Z))
            return OpenMaterialDialog(ui, uiTick, UiConstructionShape.Wall, "Wall: choose material [Z]=Stone [X]=Log");

        if (keyboard.IsKeyPressed(Keys.X))
            return OpenMaterialDialog(ui, uiTick, UiConstructionShape.Floor, "Floor: [Z]=Stone [X]=Plank");

        if (keyboard.IsKeyPressed(Keys.C))
            return OpenMaterialDialog(ui, uiTick, UiConstructionShape.Ramp, "Ramp: [ENTER]=Stone+Plank");

        if (keyboard.IsKeyPressed(Keys.V))
        {
            ui.AddToast("Stairs (multi-level) WIP", uiTick + 150);
            return true;
        }

        return false;
    }

    private static bool OpenMaterialDialog(UiStore ui, ulong uiTick, UiConstructionShape shape, string toast)
    {
        ui.SelectedConstructionShape = shape;
        ui.ResetConstructionSelection();
        ui.ConstructionMaterialDialogOpen = true;
        Logger.Log($"[BUILD.UI] Open material dialog shape={shape}");
        ui.AddToast(toast, uiTick + 200);
        return true;
    }
}
