using System;
using System.Linq;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.App.GameStates;

namespace HumanFortress.App.States
{
    public class MainMenuState : ScreenObject
    {
        private readonly ScreenSurface _surface;
        private readonly SadConsole.Console _menuConsole;
        
        public MainMenuState()
        {
            // Create root surface
            _surface = new ScreenSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            _surface.UseMouse = false;
            _surface.UseKeyboard = false;
            
            // Create menu console
            _menuConsole = new SadConsole.Console(80, 40);
            _menuConsole.Position = new Point(20, 10);
            
            // Add console to surface
            _surface.Children.Add(_menuConsole);
            
            // Add surface as child
            Children.Add(_surface);
            
            // Make this focusable
            IsFocused = true;
            UseKeyboard = true;
            UseMouse = false;
            
            DrawMenu();
        }
        
        private void DrawMenu()
        {
            _menuConsole.Clear();
            _menuConsole.Print(20, 5, "=== HUMANFORTRESS ===", Color.Yellow);
            _menuConsole.Print(20, 7, "Dwarf Fortress-like Game", Color.Gray);
            _menuConsole.Print(20, 8, "Phase B: WorldGen & WorldMap", Color.DarkGray);
            
            _menuConsole.Print(20, 12, "[N] - New World", Color.White);
            _menuConsole.Print(20, 13, "[L] - Load Game (placeholder)", Color.Gray);
            _menuConsole.Print(20, 14, "[Q] - Quit", Color.Red);
            
            _menuConsole.Print(20, 18, "Press a key to select an option", Color.DarkCyan);
        }
        
        public override bool ProcessKeyboard(Keyboard keyboard)
        {
            // Debug output
            if (keyboard.KeysPressed.Count > 0)
            {
                Logger.Log($"MainMenuState: Key pressed - {keyboard.KeysPressed.First().Key}");
            }

            if (keyboard.IsKeyPressed(Keys.N))
            {
                GameStateManager.Instance.ChangeState(GameStateType.WorldGen);
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.Q))
            {
                Environment.Exit(0);
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.L))
            {
                // Placeholder for load game
                return true;
            }
            
            return false;
        }
    }
}