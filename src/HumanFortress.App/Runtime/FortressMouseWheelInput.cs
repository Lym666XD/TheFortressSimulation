using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Runtime;

internal static class FortressMouseWheelInput
{
    public static FortressMouseWheelResult Handle(
        int scrollWheelValueChange,
        Keyboard? keyboard,
        UiStore ui,
        int zoomLevel,
        int currentZ)
    {
        ArgumentNullException.ThrowIfNull(ui);

        if (scrollWheelValueChange == 0)
            return new FortressMouseWheelResult(false, zoomLevel, currentZ);

        bool ctrlHeld = keyboard != null &&
            (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));

        int delta = scrollWheelValueChange > 0 ? 1 : -1;
        if (ctrlHeld)
        {
            int nextZoom = Math.Clamp(zoomLevel + delta, 1, 4);
            Logger.Log($"[ZOOM] Ctrl+Wheel delta={delta} -> zoom={nextZoom}");
            return new FortressMouseWheelResult(true, nextZoom, currentZ);
        }

        int nextZ = Math.Clamp(currentZ - delta, 0, 49);
        if (ui.PlaceMode == PlacementMode.MiningSecondCorner)
        {
            ui.PlaceZMax = nextZ;
            Logger.Log($"[ZLEVEL] Mining mode: updated PlaceZMax={nextZ}");
        }

        Logger.Log($"[ZLEVEL] Wheel delta={delta} (reversed) -> Z={nextZ}");
        return new FortressMouseWheelResult(true, zoomLevel, nextZ);
    }
}

internal readonly record struct FortressMouseWheelResult(bool Changed, int ZoomLevel, int CurrentZ);
