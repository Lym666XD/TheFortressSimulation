using System;
using SadConsole;
using SadRogue.Primitives;
using HumanFortress.App.GameStates;
using HumanFortress.App.Session;

namespace HumanFortress.App.States
{
    internal sealed partial class WorldMapState : ScreenObject
    {
        private readonly IAppStateNavigator _navigator;
        private readonly FortressSessionContext _session;
        private readonly ScreenSurface _mapSurface;
        private readonly SadConsole.Console _infoPanel;
        private readonly SadConsole.Console _controlsPanel;
        private Point _cameraPos;
        private Point _cursorPos;
        private const int MAP_WIDTH = 80;
        private const int MAP_HEIGHT = 40;

        internal WorldMapState(IAppStateNavigator navigator, FortressSessionContext session)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            _session = session ?? throw new ArgumentNullException(nameof(session));

            // Create a root surface for the entire screen
            var rootSurface = new ScreenSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            rootSurface.UseMouse = false;
            rootSurface.UseKeyboard = false;

            _mapSurface = new ScreenSurface(MAP_WIDTH, MAP_HEIGHT);
            _mapSurface.Position = new Point(2, 2);
            _mapSurface.UseMouse = false;
            _mapSurface.UseKeyboard = false;

            _infoPanel = new SadConsole.Console(30, 40);
            _infoPanel.Position = new Point(MAP_WIDTH + 4, 2);

            _controlsPanel = new SadConsole.Console(MAP_WIDTH, 5);
            _controlsPanel.Position = new Point(2, MAP_HEIGHT + 3);

            // Add all surfaces to the root
            rootSurface.Children.Add(_mapSurface);
            rootSurface.Children.Add(_infoPanel);
            rootSurface.Children.Add(_controlsPanel);

            // Add root as the only child
            Children.Add(rootSurface);

            // Make this ScreenObject focusable
            IsFocused = true;
            UseKeyboard = true;
            UseMouse = false;

            _cameraPos = new Point(0, 0);
            _cursorPos = new Point(MAP_WIDTH / 2, MAP_HEIGHT / 2);
            if (_session.TryGetWorldSize(out int worldWidth, out int worldHeight))
            {
                _cursorPos = new Point(
                    Math.Min(MAP_WIDTH / 2, Math.Max(0, worldWidth - 1)),
                    Math.Min(MAP_HEIGHT / 2, Math.Max(0, worldHeight - 1)));

                if (_session.TryFindNearestEmbarkableTile(_cursorPos, out var embarkableTile))
                {
                    _cursorPos = embarkableTile;
                    CenterCameraOnCursor(worldWidth, worldHeight);
                }
            }

            DrawControls();
            RenderMap();
        }
        
        private void DrawControls()
        {
            _controlsPanel.Clear();
            _controlsPanel.Print(0, 0, "=== WORLD MAP ===", Color.Yellow);
            _controlsPanel.Print(0, 1, "WASD/Arrows - Move Cursor | Enter - Embark | ESC - Menu", Color.Gray);
            _controlsPanel.Print(0, 2, "Shift - Fast movement | Ctrl+Move - Camera | E - Find embark", Color.Gray);
        }
        
    }
}
