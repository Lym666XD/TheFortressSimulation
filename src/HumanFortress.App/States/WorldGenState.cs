using System;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.Core.World;
using HumanFortress.WorldGen;
using HumanFortress.App.GameStates;

namespace HumanFortress.App.States
{
    public class WorldGenState : ScreenObject
    {
        private readonly ScreenSurface _surface;
        private readonly SadConsole.Console _paramsConsole;
        private readonly SadConsole.Console _progressConsole;
        private WorldParams _params;
        private WorldGenerator _generator;
        private bool _isGenerating;
        private WorldGenResult _result;
        private int _selectedOption = 0;
        
        public WorldGenState()
        {
            // Create main surface that fills the screen
            _surface = new ScreenSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            _surface.UseMouse = false;
            _surface.UseKeyboard = false;

            _paramsConsole = new SadConsole.Console(60, 30);
            _paramsConsole.Position = new Point(10, 5);

            _progressConsole = new SadConsole.Console(60, 10);
            _progressConsole.Position = new Point(10, 36);

            // Add consoles to the surface, not to this ScreenObject
            _surface.Children.Add(_paramsConsole);
            _surface.Children.Add(_progressConsole);

            // Add the surface as the only child
            Children.Add(_surface);

            // Make this ScreenObject focusable
            IsFocused = true;
            UseKeyboard = true;
            UseMouse = false;

            _params = WorldParams.Default;
            _generator = new WorldGenerator();
            _generator.ProgressChanged += OnProgressChanged;

            DrawUI();
        }
        
        private void DrawUI()
        {
            _paramsConsole.Clear();
            _paramsConsole.Print(0, 0, "=== WORLD GENERATION ===", Color.Yellow);
            
            _paramsConsole.Print(0, 2, "World Parameters:", Color.Cyan);
            
            var nameColor = _selectedOption == 0 ? Color.White : Color.Gray;
            _paramsConsole.Print(2, 4, $"Name: {_params.Name}", nameColor);
            
            var seedColor = _selectedOption == 1 ? Color.White : Color.Gray;
            _paramsConsole.Print(2, 5, $"Seed: {_params.Seed}", seedColor);
            
            var sizeColor = _selectedOption == 2 ? Color.White : Color.Gray;
            _paramsConsole.Print(2, 6, $"Size: {_params.Width}x{_params.Height}", sizeColor);
            
            var diffColor = _selectedOption == 3 ? Color.White : Color.Gray;
            _paramsConsole.Print(2, 7, $"Difficulty: {_params.Difficulty}", diffColor);
            
            _paramsConsole.Print(0, 10, "Controls:", Color.Cyan);
            _paramsConsole.Print(2, 11, "↑/↓ - Select option", Color.Gray);
            _paramsConsole.Print(2, 12, "←/→ - Modify value", Color.Gray);
            _paramsConsole.Print(2, 13, "R - Random seed", Color.Gray);
            _paramsConsole.Print(2, 14, "Enter - Generate World", Color.Green);
            _paramsConsole.Print(2, 15, "ESC - Back to Menu", Color.Red);
        }
        
        private void OnProgressChanged(string stage, float progress)
        {
            _progressConsole.Clear();
            _progressConsole.Print(0, 0, "Generation Progress:", Color.Cyan);
            _progressConsole.Print(0, 2, stage, Color.White);
            
            int barWidth = 50;
            int filled = (int)(barWidth * progress);
            string bar = "[" + new string('=', filled) + new string('-', barWidth - filled) + "]";
            _progressConsole.Print(0, 4, bar, Color.Green);
            _progressConsole.Print(0, 5, $"{(int)(progress * 100)}%", Color.White);
        }
        
        public override bool ProcessKeyboard(Keyboard keyboard)
        {
            if (_isGenerating)
                return true;
            
            if (keyboard.IsKeyPressed(Keys.Escape))
            {
                GameStateManager.Instance.ChangeState(GameStateType.MainMenu);
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
                _selectedOption = Math.Min(3, _selectedOption + 1);
                DrawUI();
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.Left) || keyboard.IsKeyPressed(Keys.Right))
            {
                ModifySelectedOption(keyboard.IsKeyPressed(Keys.Right));
                DrawUI();
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.R))
            {
                _params.Seed = (uint)Environment.TickCount;
                DrawUI();
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.Enter))
            {
                StartGeneration();
                return true;
            }

            return false;
        }
        
        private void ModifySelectedOption(bool increase)
        {
            switch (_selectedOption)
            {
                case 1:
                    if (increase)
                        _params.Seed++;
                    else
                        _params.Seed--;
                    break;
                case 2:
                    int[] sizes = { 128, 256, 512 };
                    int currentIndex = Array.IndexOf(sizes, _params.Width);
                    if (currentIndex == -1) currentIndex = 1;
                    
                    if (increase)
                        currentIndex = Math.Min(2, currentIndex + 1);
                    else
                        currentIndex = Math.Max(0, currentIndex - 1);
                    
                    _params.Width = sizes[currentIndex];
                    _params.Height = sizes[currentIndex];
                    break;
                case 3:
                    int diffValue = (int)_params.Difficulty;
                    if (increase)
                        diffValue = Math.Min(3, diffValue + 1);
                    else
                        diffValue = Math.Max(0, diffValue - 1);
                    _params.Difficulty = (DifficultyPreset)diffValue;
                    break;
            }
        }
        
        private void StartGeneration()
        {
            _isGenerating = true;
            _progressConsole.Clear();
            _progressConsole.Print(0, 0, "Starting world generation...", Color.Yellow);
            
            _result = _generator.Generate(_params);
            
            if (_result.Success)
            {
                WorldMapState.CurrentWorld = _result;
                GameStateManager.Instance.ChangeState(GameStateType.WorldMap);
            }
            else
            {
                _progressConsole.Print(0, 6, "Generation failed!", Color.Red);
                _isGenerating = false;
            }
        }
    }
}