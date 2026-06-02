using System;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.Core.World;
using HumanFortress.App.Runtime;
using HumanFortress.WorldGen;

namespace HumanFortress.App.States
{
    public class EmbarkPrepState : ScreenObject
    {
        private readonly IAppStateNavigator _navigator;
        private readonly FortressSessionContext _session;
        private readonly SadConsole.Console _mainConsole;
        private int _fortressSize = 2;
        private readonly int[] _sizeOptions = { 2, 3, 4, 5, 6, 7, 8 }; // Per MILESTONE.md: N∈[2..8]
        private int _selectedOption = 0; // Default to 2x2
        
        private Point SelectedTile => _session.SelectedTile;
        private WorldGenResult CurrentWorld => _session.CurrentWorld;

        public EmbarkPrepState(IAppStateNavigator navigator, FortressSessionContext session)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            _session = session ?? throw new ArgumentNullException(nameof(session));

            // Create a root surface
            var rootSurface = new ScreenSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            rootSurface.UseMouse = false;
            rootSurface.UseKeyboard = false;

            _mainConsole = new SadConsole.Console(80, 50);
            _mainConsole.Position = new Point(10, 5);
            _mainConsole.UseMouse = false;
            _mainConsole.UseKeyboard = false;

            // Add console to root surface
            rootSurface.Children.Add(_mainConsole);

            // Add root as the only child
            Children.Add(rootSurface);

            // Make this ScreenObject focusable
            IsFocused = true;
            UseKeyboard = true;
            UseMouse = false;

            DrawUI();
        }
        
        private void DrawUI()
        {
            _mainConsole.Clear();
            _mainConsole.Print(0, 0, "=== EMBARK PREPARATION ===", Color.Yellow);
            
            _mainConsole.Print(0, 2, $"Selected Location: {SelectedTile.X},{SelectedTile.Y}", Color.Cyan);
            
            if (CurrentWorld.Tiles != null &&
                SelectedTile.X >= 0 &&
                SelectedTile.Y >= 0 &&
                SelectedTile.X < CurrentWorld.Tiles.GetLength(0) &&
                SelectedTile.Y < CurrentWorld.Tiles.GetLength(1))
            {
                var tile = CurrentWorld.Tiles[SelectedTile.X, SelectedTile.Y];
                BiomeType biome = (BiomeType)tile.BiomeId;
                _mainConsole.Print(0, 3, $"Biome: {biome}", Color.Green);
            }
            
            _mainConsole.Print(0, 5, "Fortress Settings:", Color.Cyan);
            
            var sizeColor = _selectedOption == 0 ? Color.White : Color.Gray;
            _mainConsole.Print(2, 7, $"Size: {_fortressSize}x{_fortressSize} chunks", sizeColor);
            _mainConsole.Print(2, 8, $"Total tiles: {_fortressSize * 32}x{_fortressSize * 32}", Color.DarkGray);
            
            _mainConsole.Print(0, 10, "Starting Resources:", Color.Cyan);
            _mainConsole.Print(2, 11, "7 Dwarves (placeholder)", Color.Gray);
            _mainConsole.Print(2, 12, "Food: 100 units", Color.Gray);
            _mainConsole.Print(2, 13, "Drink: 50 units", Color.Gray);
            _mainConsole.Print(2, 14, "Seeds: 20 units", Color.Gray);
            _mainConsole.Print(2, 15, "Tools: Basic set", Color.Gray);
            
            _mainConsole.Print(0, 18, "Controls:", Color.Cyan);
            _mainConsole.Print(2, 19, "↑/↓ - Select option", Color.Gray);
            _mainConsole.Print(2, 20, "←/→ - Modify value", Color.Gray);
            _mainConsole.Print(2, 21, "Enter - Embark!", Color.Green);
            _mainConsole.Print(2, 22, "ESC - Back to World Map", Color.Red);
            
            _mainConsole.Print(0, 25, "Note: Fortress generation will create:", Color.DarkCyan);
            _mainConsole.Print(2, 26, $"- {_fortressSize}x{_fortressSize} chunks", Color.DarkGray);
            _mainConsole.Print(2, 27, "- 50 Z-levels", Color.DarkGray);
            _mainConsole.Print(2, 28, "- Surface terrain based on biome", Color.DarkGray);
            _mainConsole.Print(2, 29, "- One cavern layer", Color.DarkGray);
            _mainConsole.Print(2, 30, "- Ore veins and resources", Color.DarkGray);
        }
        
        public override bool ProcessKeyboard(Keyboard keyboard)
        {
            // Debug: Log that we're receiving input
            System.Console.WriteLine($"EmbarkPrepState ProcessKeyboard called, HasKeyPressed: {keyboard.KeysPressed.Count > 0}");

            if (keyboard.IsKeyPressed(Keys.Escape))
            {
                _navigator.ShowWorldMap();
                return true;
            }
            
            if (keyboard.IsKeyPressed(Keys.Up))
            {
                _selectedOption = Math.Max(0, _selectedOption - 1);
                DrawUI();
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.Down))
            {
                _selectedOption = Math.Min(0, _selectedOption + 1);  // Currently only one option, but keep for future
                DrawUI();
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.Left) || keyboard.IsKeyPressed(Keys.Right))
            {
                if (_selectedOption == 0)
                {
                    int currentIndex = Array.IndexOf(_sizeOptions, _fortressSize);
                    if (keyboard.IsKeyPressed(Keys.Right))
                        currentIndex = Math.Min(_sizeOptions.Length - 1, currentIndex + 1);
                    else
                        currentIndex = Math.Max(0, currentIndex - 1);
                    _fortressSize = _sizeOptions[currentIndex];
                    DrawUI();
                }
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.Enter))
            {
                StartEmbark();
                return true;
            }

            return false;
        }
        
        private void StartEmbark()
        {
            // Validate fortress size to prevent crashes
            if (_fortressSize < 2 || _fortressSize > 8)
            {
                System.Console.WriteLine($"[EmbarkPrepState] WARNING: Invalid fortress size {_fortressSize}, defaulting to 2");
                _fortressSize = 2;
            }

            System.Console.WriteLine($"[EmbarkPrepState] Starting embark at {SelectedTile} with size {_fortressSize}x{_fortressSize}");
            _session.ConfigureEmbark(SelectedTile, _fortressSize);

            System.Console.WriteLine("[EmbarkPrepState] Changing state to FortressPlay");
            _navigator.ShowFortressPlay();
        }
    }
}
