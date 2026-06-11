using HumanFortress.App.UI;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal static class FortressScreenLayoutFactory
{
    public static FortressScreenLayout Create(GameHost gameHost)
    {
        ArgumentNullException.ThrowIfNull(gameHost);

        int screenW = gameHost.ScreenCellsX;
        int screenH = gameHost.ScreenCellsY;

        var rootSurface = new ScreenSurface(screenW, screenH)
        {
            UseMouse = true,
            UseKeyboard = false
        };
        Logger.Log("[FortressState] Root surface created");

        int mapW = Math.Max(20, screenW - 4);
        int mapH = Math.Max(8, screenH - 4);
        var mapSurface = new MapScreenSurface(mapW, mapH)
        {
            Position = new Point(2, 2),
            UseMouse = true,
            UseKeyboard = false
        };
        Logger.Log("[FortressState] Map surface created");
        Logger.Log($"[INIT] MapSurface size={mapSurface.Surface.Width}x{mapSurface.Surface.Height} pos={mapSurface.Position}");

        var uiSurface = new UiOverlaySurface(screenW, screenH)
        {
            UseMouse = true,
            UseKeyboard = true,
            FocusOnMouseClick = false
        };

        rootSurface.Children.Add(mapSurface);
        rootSurface.Children.Add(uiSurface);

        return new FortressScreenLayout(rootSurface, mapSurface, uiSurface);
    }
}
