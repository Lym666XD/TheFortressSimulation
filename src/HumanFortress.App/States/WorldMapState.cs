using System;
using System.Linq;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.Core.World;
using HumanFortress.WorldGen;
using HumanFortress.App.Runtime;

namespace HumanFortress.App.States
{
    public class WorldMapState : ScreenObject
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
        
        private WorldGenResult CurrentWorld => _session.CurrentWorld;

        public WorldMapState(IAppStateNavigator navigator, FortressSessionContext session)
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

            DrawControls();
            RenderMap();
        }
        
        private void DrawControls()
        {
            _controlsPanel.Clear();
            _controlsPanel.Print(0, 0, "=== WORLD MAP ===", Color.Yellow);
            _controlsPanel.Print(0, 1, "WASD/Arrows - Move Camera | Enter - Embark | ESC - Menu", Color.Gray);
            _controlsPanel.Print(0, 2, "Shift - Fast movement | Tab - Toggle info", Color.Gray);
        }
        
        private void RenderMap()
        {
            if (CurrentWorld.Tiles == null)
                return;
            
            _mapSurface.Clear();
            
            int worldWidth = CurrentWorld.Tiles.GetLength(0);
            int worldHeight = CurrentWorld.Tiles.GetLength(1);
            
            for (int sx = 0; sx < MAP_WIDTH; sx++)
            {
                for (int sy = 0; sy < MAP_HEIGHT; sy++)
                {
                    int wx = _cameraPos.X + sx;
                    int wy = _cameraPos.Y + sy;
                    
                    if (wx >= 0 && wx < worldWidth && wy >= 0 && wy < worldHeight)
                    {
                        var tile = CurrentWorld.Tiles[wx, wy];
                        var (glyph, color) = GetTileDisplay(tile);
                        _mapSurface.SetGlyph(sx, sy, glyph, color);
                    }
                }
            }
            
            int cursorScreenX = _cursorPos.X - _cameraPos.X;
            int cursorScreenY = _cursorPos.Y - _cameraPos.Y;
            if (cursorScreenX >= 0 && cursorScreenX < MAP_WIDTH && 
                cursorScreenY >= 0 && cursorScreenY < MAP_HEIGHT)
            {
                var existing = _mapSurface.GetGlyph(cursorScreenX, cursorScreenY);
                _mapSurface.SetGlyph(cursorScreenX, cursorScreenY, existing, Color.Yellow, Color.DarkGray);
            }
            
            UpdateInfoPanel();
        }
        
        private (int glyph, Color color) GetTileDisplay(WorldTile tile)
        {
            BiomeType biome = (BiomeType)tile.BiomeId;
            
            return biome switch
            {
                BiomeType.Ocean => ('~', Color.DarkBlue),
                BiomeType.Lake => ('~', Color.Blue),
                BiomeType.River => ('~', Color.Cyan),
                BiomeType.Mountain => ('^', Color.Gray),
                BiomeType.Hills => ('n', Color.Brown),
                BiomeType.Desert => ('.', Color.Yellow),
                BiomeType.Tundra => ('.', Color.White),
                BiomeType.Glacier => ('#', Color.Cyan),
                BiomeType.TemperateForest => ('T', Color.Green),
                BiomeType.TropicalForest => ('T', Color.DarkGreen),
                BiomeType.Taiga => ('t', Color.DarkGreen),
                BiomeType.TemperateGrassland => ('.', Color.LightGreen),
                BiomeType.Savanna => (':', Color.YellowGreen),
                BiomeType.Swamp => ('%', Color.DarkGreen),
                _ => ('?', Color.Magenta)
            };
        }
        
        private void UpdateInfoPanel()
        {
            _infoPanel.Clear();
            _infoPanel.Print(0, 0, "=== TILE INFO ===", Color.Yellow);
            
            if (CurrentWorld.Tiles == null)
                return;
            
            int worldWidth = CurrentWorld.Tiles.GetLength(0);
            int worldHeight = CurrentWorld.Tiles.GetLength(1);
            
            if (_cursorPos.X >= 0 && _cursorPos.X < worldWidth &&
                _cursorPos.Y >= 0 && _cursorPos.Y < worldHeight)
            {
                var tile = CurrentWorld.Tiles[_cursorPos.X, _cursorPos.Y];
                BiomeType biome = (BiomeType)tile.BiomeId;
                
                _infoPanel.Print(0, 2, $"Position: {_cursorPos.X},{_cursorPos.Y}", Color.White);
                _infoPanel.Print(0, 3, $"Biome: {biome}", Color.Cyan);
                _infoPanel.Print(0, 5, $"Elevation: {tile.Elevation:F2}", Color.Gray);
                _infoPanel.Print(0, 6, $"Temperature: {tile.Temperature:F2}", Color.Orange);
                _infoPanel.Print(0, 7, $"Rainfall: {tile.Rainfall:F2}", Color.Blue);
                _infoPanel.Print(0, 8, $"Drainage: {tile.Drainage:F2}", Color.Brown);
                
                if (tile.IsEmbarkable)
                {
                    _infoPanel.Print(0, 10, "[EMBARKABLE]", Color.Green);
                    _infoPanel.Print(0, 11, "Press Enter to embark", Color.Gray);
                }
                else
                {
                    _infoPanel.Print(0, 10, "[NOT EMBARKABLE]", Color.Red);
                }
            }
        }
        
        public override bool ProcessKeyboard(Keyboard keyboard)
        {
            bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            int moveSpeed = shift ? 10 : 1;

            bool moved = false;
            bool cursorMoved = false;

            // Handle cursor movement with WASD
            if (!keyboard.IsKeyDown(Keys.LeftControl) && !keyboard.IsKeyDown(Keys.RightControl))
            {
                if (keyboard.IsKeyPressed(Keys.W))
                {
                    _cursorPos = new Point(_cursorPos.X, Math.Max(0, _cursorPos.Y - moveSpeed));
                    cursorMoved = true;
                }
                else if (keyboard.IsKeyPressed(Keys.S))
                {
                    _cursorPos = new Point(_cursorPos.X, Math.Min(CurrentWorld.Tiles.GetLength(1) - 1, _cursorPos.Y + moveSpeed));
                    cursorMoved = true;
                }
                else if (keyboard.IsKeyPressed(Keys.A))
                {
                    _cursorPos = new Point(Math.Max(0, _cursorPos.X - moveSpeed), _cursorPos.Y);
                    cursorMoved = true;
                }
                else if (keyboard.IsKeyPressed(Keys.D))
                {
                    _cursorPos = new Point(Math.Min(CurrentWorld.Tiles.GetLength(0) - 1, _cursorPos.X + moveSpeed), _cursorPos.Y);
                    cursorMoved = true;
                }
            }

            // Handle camera movement with arrow keys or Ctrl+WASD
            if (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl))
            {
                if (keyboard.IsKeyPressed(Keys.W) || keyboard.IsKeyPressed(Keys.Up))
                {
                    _cameraPos = new Point(_cameraPos.X, Math.Max(0, _cameraPos.Y - moveSpeed));
                    moved = true;
                }
                else if (keyboard.IsKeyPressed(Keys.S) || keyboard.IsKeyPressed(Keys.Down))
                {
                    _cameraPos = new Point(_cameraPos.X, Math.Min(CurrentWorld.Tiles.GetLength(1) - MAP_HEIGHT, _cameraPos.Y + moveSpeed));
                    moved = true;
                }
                else if (keyboard.IsKeyPressed(Keys.A) || keyboard.IsKeyPressed(Keys.Left))
                {
                    _cameraPos = new Point(Math.Max(0, _cameraPos.X - moveSpeed), _cameraPos.Y);
                    moved = true;
                }
                else if (keyboard.IsKeyPressed(Keys.D) || keyboard.IsKeyPressed(Keys.Right))
                {
                    _cameraPos = new Point(Math.Min(CurrentWorld.Tiles.GetLength(0) - MAP_WIDTH, _cameraPos.X + moveSpeed), _cameraPos.Y);
                    moved = true;
                }
            }
            else if (keyboard.IsKeyPressed(Keys.Up) || keyboard.IsKeyPressed(Keys.Down) ||
                     keyboard.IsKeyPressed(Keys.Left) || keyboard.IsKeyPressed(Keys.Right))
            {
                // Arrow keys without Ctrl move cursor
                if (keyboard.IsKeyPressed(Keys.Up))
                {
                    _cursorPos = new Point(_cursorPos.X, Math.Max(0, _cursorPos.Y - moveSpeed));
                    cursorMoved = true;
                }
                else if (keyboard.IsKeyPressed(Keys.Down))
                {
                    _cursorPos = new Point(_cursorPos.X, Math.Min(CurrentWorld.Tiles.GetLength(1) - 1, _cursorPos.Y + moveSpeed));
                    cursorMoved = true;
                }
                else if (keyboard.IsKeyPressed(Keys.Left))
                {
                    _cursorPos = new Point(Math.Max(0, _cursorPos.X - moveSpeed), _cursorPos.Y);
                    cursorMoved = true;
                }
                else if (keyboard.IsKeyPressed(Keys.Right))
                {
                    _cursorPos = new Point(Math.Min(CurrentWorld.Tiles.GetLength(0) - 1, _cursorPos.X + moveSpeed), _cursorPos.Y);
                    cursorMoved = true;
                }
            }

            // Update camera to follow cursor
            if (cursorMoved)
            {
                // Center camera on cursor
                int newCameraX = _cursorPos.X - MAP_WIDTH / 2;
                int newCameraY = _cursorPos.Y - MAP_HEIGHT / 2;

                // Clamp camera position
                newCameraX = Math.Max(0, Math.Min(CurrentWorld.Tiles.GetLength(0) - MAP_WIDTH, newCameraX));
                newCameraY = Math.Max(0, Math.Min(CurrentWorld.Tiles.GetLength(1) - MAP_HEIGHT, newCameraY));

                _cameraPos = new Point(newCameraX, newCameraY);
                moved = true;
            }

            if (moved || cursorMoved)
            {
                RenderMap();
            }
            
            if (keyboard.IsKeyPressed(Keys.Enter))
            {
                var tile = CurrentWorld.Tiles[_cursorPos.X, _cursorPos.Y];
                if (tile.IsEmbarkable)
                {
                    _session.SelectEmbarkTile(new Point(_cursorPos.X, _cursorPos.Y));
                    _navigator.ShowEmbarkPreparation();
                }
            }
            else if (keyboard.IsKeyPressed(Keys.Escape))
            {
                _navigator.ShowMainMenu();
            }
            
            return true;
        }
    }
}
