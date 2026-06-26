using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressKeyboardInputRouter
{
    private static FortressKeyboardInputResult Redraw(FortressKeyboardInputRouterContext context)
    {
        return new FortressKeyboardInputResult(true, true, context.Viewport.CameraPosition, context.Viewport.CurrentZ);
    }

    private static bool ApplyNavigation(
        FortressKeyboardInputRouterContext context,
        Keyboard keyboard,
        ref Point cameraPosition,
        ref int currentZ)
    {
        int previousZ = currentZ;
        var navigationInput = FortressKeyboardNavigationInput.Handle(keyboard, cameraPosition, currentZ, context.SelectionTool);
        cameraPosition = navigationInput.CameraPosition;
        currentZ = navigationInput.CurrentZ;

        if (currentZ != previousZ)
            FortressMiningZRangeSync.ApplyCurrentZ(context.Ui, context.SelectionTool, currentZ);

        return navigationInput.Changed;
    }
}
