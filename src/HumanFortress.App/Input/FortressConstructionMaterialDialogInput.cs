using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static class FortressConstructionMaterialDialogInput
{
    private static readonly string[] RampMaterialTags = { "stone_block", "wood_plank" };

    public static bool Handle(Keyboard keyboard, UiStore ui, int currentZ, ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        if (!ui.ConstructionMaterialDialogOpen)
            return false;

        var shape = ui.SelectedConstructionShape;
        if (keyboard.IsKeyPressed(Keys.Escape))
        {
            ui.ConstructionMaterialDialogOpen = false;
            return true;
        }

        if (shape == UiConstructionShape.Wall)
        {
            if (keyboard.IsKeyPressed(Keys.Z))
                return SelectMaterial(ui, currentZ, uiTick, "stone_block", "Wall: Stone Block");

            if (keyboard.IsKeyPressed(Keys.X))
                return SelectMaterial(ui, currentZ, uiTick, "wood_log", "Wall: Wood Log");
        }
        else if (shape == UiConstructionShape.Floor)
        {
            if (keyboard.IsKeyPressed(Keys.Z))
                return SelectMaterial(ui, currentZ, uiTick, "stone_block", "Floor: Stone Block");

            if (keyboard.IsKeyPressed(Keys.X))
                return SelectMaterial(ui, currentZ, uiTick, "wood_plank", "Floor: Wood Plank");
        }
        else if (shape == UiConstructionShape.Ramp)
        {
            if (keyboard.IsKeyPressed(Keys.Enter) || keyboard.IsKeyPressed(Keys.Z))
                return SelectMaterials(ui, currentZ, uiTick, RampMaterialTags, "Ramp: Stone+Plank");
        }

        return false;
    }

    private static bool SelectMaterial(UiStore ui, int currentZ, ulong uiTick, string tag, string toast)
    {
        ui.ConstructionSelectedTags.Clear();
        ui.ConstructionSelectedTags.Add(tag);
        Logger.Log($"[BUILD.UI] Selected tags=[{tag}]");
        ui.ConstructionMaterialDialogOpen = false;
        ui.StartPlacement(PlacementMode.ConstructionFirstCorner, currentZ);
        ui.AddToast(toast, uiTick + 100);
        return true;
    }

    private static bool SelectMaterials(UiStore ui, int currentZ, ulong uiTick, IReadOnlyList<string> tags, string toast)
    {
        ui.ConstructionSelectedTags.Clear();
        foreach (var tag in tags)
            ui.ConstructionSelectedTags.Add(tag);

        Logger.Log($"[BUILD.UI] Selected tags=[{string.Join("|", tags)}]");
        ui.ConstructionMaterialDialogOpen = false;
        ui.StartPlacement(PlacementMode.ConstructionFirstCorner, currentZ);
        ui.AddToast(toast, uiTick + 100);
        return true;
    }
}
