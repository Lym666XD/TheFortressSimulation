using HumanFortress.App.UI;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static class FortressDebugUnitOverlayRenderer
{
    public static void Draw(ScreenSurface uiSurface, UiStore ui, int cameraX, int cameraY, int currentZ)
    {
        var surf = uiSurface.Surface;
        foreach (var (position, z) in ui.DebugDwarfs)
        {
            if (z != currentZ)
                continue;

            int screenX = position.X - cameraX;
            int screenY = position.Y - cameraY;
            if (screenX >= 0 && screenY >= 0 && screenX < surf.Width && screenY < surf.Height)
            {
                surf.SetGlyph(screenX, screenY, 'D', Color.Yellow, new Color(0, 0, 0));
            }
        }
    }
}
