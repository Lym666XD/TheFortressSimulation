using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using HumanFortress.Contracts.Runtime;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static class FortressMouseWheelInput
{
    public static FortressMouseWheelResult Handle(
        int scrollWheelValueChange,
        Keyboard? keyboard,
        UiStore ui,
        ISelectionTool? selectionTool,
        int zoomLevel,
        int currentZ,
        RuntimeWorldBounds worldBounds)
    {
        ArgumentNullException.ThrowIfNull(ui);

        if (scrollWheelValueChange == 0)
            return new FortressMouseWheelResult(false, zoomLevel, currentZ);

        bool ctrlHeld = keyboard != null &&
            (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));
        bool shiftHeld = keyboard != null &&
            (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));

        int delta = scrollWheelValueChange > 0 ? 1 : -1;

        if (selectionTool != null && selectionTool.IsActive && shiftHeld)
        {
            selectionTool.AdjustZRange(delta);
            Logger.Log($"[SELECT] z-range {(delta > 0 ? "+" : "-")}{Math.Abs(delta)}");
            return new FortressMouseWheelResult(true, zoomLevel, currentZ);
        }

        if (ctrlHeld)
        {
            int nextZoom = Math.Clamp(zoomLevel + delta, 1, 4);
            Logger.Log($"[ZOOM-UPDATE] delta={delta} -> zoom={nextZoom}");
            return new FortressMouseWheelResult(true, nextZoom, currentZ);
        }

        int nextZ = worldBounds.IsEmpty
            ? 0
            : Math.Clamp(currentZ - delta, worldBounds.MinZ, worldBounds.MaxZExclusive - 1);
        FortressMiningZRangeSync.ApplyCurrentZ(ui, selectionTool, nextZ);

        Logger.Log($"[ZLEVEL-UPDATE] delta={delta} -> Z={nextZ}");
        return new FortressMouseWheelResult(true, zoomLevel, nextZ);
    }
}

internal readonly record struct FortressMouseWheelResult(bool Changed, int ZoomLevel, int CurrentZ);
