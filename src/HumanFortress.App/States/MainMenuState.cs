using System;
using System.Linq;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.App.Runtime;
using HumanFortress.App.UI;

namespace HumanFortress.App.States
{
    public class MainMenuState : ScreenObject
    {
        private readonly ScreenSurface _surface;
        private readonly MenuSurface _menuSurface;
        private readonly IAppStateNavigator _navigator;
        private readonly UiStore _uiStore = new UiStore();
        private ulong _tick = 0;

        private enum MenuItem
        {
            NewWorld = 0,
            LoadWorld = 1,
            Settings = 2,
            Credits = 3,
            Exit = 4
        }

        private enum PageMode
        {
            MainMenu,
            Settings,
            Credits
        }

        private MenuItem _selectedItem = MenuItem.NewWorld;
        private MenuItem? _hoveredItem = null;
        private PageMode _currentPage = PageMode.MainMenu;
        private const int MENU_START_Y = 22;
        private const int MENU_ITEM_HEIGHT = 2;
        private const int MENU_WIDTH = 30;

        public MainMenuState(IAppStateNavigator navigator)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

            // Create root surface
            _surface = new ScreenSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            _surface.UseMouse = false;
            _surface.UseKeyboard = false;

            // Create menu surface with mouse support - same pattern as MapScreenSurface
            _menuSurface = new MenuSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            _menuSurface.Position = new Point(0, 0);

            // Hook up mouse events
            _menuSurface.MouseMovedLocal += OnMouseMoved;
            _menuSurface.LeftClickedLocal += OnMouseClicked;
            _menuSurface.RightClickedLocal += OnRightClicked;

            // Add menu surface to root surface
            _surface.Children.Add(_menuSurface);

            // Add surface as child
            Children.Add(_surface);

            // Make this focusable for keyboard
            IsFocused = true;
            UseKeyboard = true;
            UseMouse = false; // Let child handle mouse

            DrawMenu();
        }

        private void OnMouseMoved(Point local)
        {
            // Only handle mouse on main menu
            if (_currentPage != PageMode.MainMenu)
                return;

            var newHoveredItem = GetMenuItemAtPosition(local);

            if (newHoveredItem != _hoveredItem)
            {
                _hoveredItem = newHoveredItem;
                DrawMenu();
            }
        }

        private void OnMouseClicked(Point local)
        {
            // Only handle mouse on main menu
            if (_currentPage != PageMode.MainMenu)
                return;

            var clickedItem = GetMenuItemAtPosition(local);
            if (clickedItem.HasValue)
            {
                _selectedItem = clickedItem.Value;
                ExecuteMenuItem(_selectedItem);
            }
        }

        private void OnRightClicked(Point local)
        {
            // Right-click works like ESC - go back or exit
            if (_currentPage != PageMode.MainMenu)
            {
                _currentPage = PageMode.MainMenu;
                DrawMenu();
            }
            else
            {
                Environment.Exit(0);
            }
        }

        public override void Update(TimeSpan delta)
        {
            base.Update(delta);
            _tick++;

            // Update menu if needed for animations
            if (_tick % 20 == 0)
            {
                DrawMenu();
            }
        }

        private void DrawMenu()
        {
            _menuSurface.Surface.Clear();

            switch (_currentPage)
            {
                case PageMode.MainMenu:
                    DrawMainMenu();
                    break;
                case PageMode.Settings:
                    DrawSettingsPage();
                    break;
                case PageMode.Credits:
                    DrawCreditsPage();
                    break;
            }
        }

        private void DrawMainMenu()
        {
            int centerX = _menuSurface.Surface.Width / 2;
            int centerY = _menuSurface.Surface.Height / 2;

            // Draw left knight (simplified)
            DrawLeftKnight(5, centerY - 6);

            // Draw right knight (simplified)
            DrawRightKnight(_menuSurface.Surface.Width - 15, centerY - 6);

            // Draw title (simplified ASCII art)
            DrawTitle(centerX, 3);

            // Draw menu items
            DrawMenuItems(centerX, MENU_START_Y);

            // Draw bottom hint
            bool blink = (_tick / 30) % 2 == 0;
            var hintColor = blink ? Color.Cyan : Color.DarkCyan;
            _menuSurface.Surface.Print(centerX - 25, _menuSurface.Surface.Height - 3, "Use Arrow Keys or Mouse to select | Enter to confirm", hintColor);

            // Draw toasts
            DrawToasts();
        }

        private void DrawSettingsPage()
        {
            int centerX = _menuSurface.Surface.Width / 2;

            _menuSurface.Surface.Print(centerX - 10, 3, "=== SETTINGS ===", Color.Gold);

            _menuSurface.Surface.Print(centerX - 20, 8, "Video Settings:", Color.Cyan);
            _menuSurface.Surface.Print(centerX - 18, 10, "Resolution: 1920x1080 (WIP)", Color.Gray);
            _menuSurface.Surface.Print(centerX - 18, 11, "Fullscreen: On (WIP)", Color.Gray);
            _menuSurface.Surface.Print(centerX - 18, 12, "VSync: On (WIP)", Color.Gray);

            _menuSurface.Surface.Print(centerX - 20, 15, "Audio Settings:", Color.Cyan);
            _menuSurface.Surface.Print(centerX - 18, 17, "Master Volume: 100% (WIP)", Color.Gray);
            _menuSurface.Surface.Print(centerX - 18, 18, "Music Volume: 80% (WIP)", Color.Gray);
            _menuSurface.Surface.Print(centerX - 18, 19, "SFX Volume: 90% (WIP)", Color.Gray);

            _menuSurface.Surface.Print(centerX - 20, 22, "Gameplay Settings:", Color.Cyan);
            _menuSurface.Surface.Print(centerX - 18, 24, "Auto-Save: On (WIP)", Color.Gray);
            _menuSurface.Surface.Print(centerX - 18, 25, "Difficulty: Normal (WIP)", Color.Gray);

            _menuSurface.Surface.Print(centerX - 15, _menuSurface.Surface.Height - 5, "Press ESC to return to menu", Color.Yellow);
        }

        private void DrawCreditsPage()
        {
            int centerX = _menuSurface.Surface.Width / 2;

            _menuSurface.Surface.Print(centerX - 10, 3, "=== CREDITS ===", Color.Gold);

            _menuSurface.Surface.Print(centerX - 15, 8, "HUMANFORTRESS", Color.Yellow);
            _menuSurface.Surface.Print(centerX - 22, 9, "A Dwarf Fortress-like Colony Simulation", Color.Gray);

            _menuSurface.Surface.Print(centerX - 10, 12, "Developed by:", Color.Cyan);
            _menuSurface.Surface.Print(centerX - 5, 14, "lym666", Color.White);

            _menuSurface.Surface.Print(centerX - 15, 17, "AI Programming Assistant:", Color.Cyan);
            _menuSurface.Surface.Print(centerX - 8, 19, "Claude Code", Color.White);
            _menuSurface.Surface.Print(centerX - 20, 20, "(Anthropic - Claude Sonnet 4.5)", Color.DarkGray);

            _menuSurface.Surface.Print(centerX - 10, 24, "Special Thanks:", Color.Cyan);
            _menuSurface.Surface.Print(centerX - 15, 26, "SadConsole Framework", Color.Gray);
            _menuSurface.Surface.Print(centerX - 18, 27, "Dwarf Fortress (Inspiration)", Color.Gray);
            _menuSurface.Surface.Print(centerX - 12, 28, "RimWorld (Inspiration)", Color.Gray);

            _menuSurface.Surface.Print(centerX - 15, _menuSurface.Surface.Height - 5, "Press ESC to return to menu", Color.Yellow);
        }

        private void DrawLeftKnight(int x, int y)
        {
            var knightColor = new Color(180, 180, 200);
            var swordColor = new Color(200, 200, 220);

            // Simplified knight using basic ASCII
            _menuSurface.Surface.Print(x + 2, y + 0, "[O]", knightColor);
            _menuSurface.Surface.Print(x + 1, y + 1, "/[#]\\", knightColor);
            _menuSurface.Surface.Print(x + 0, y + 2, "/ |#| \\", knightColor);
            _menuSurface.Surface.Print(x + 1, y + 3, "|   |", knightColor);
            _menuSurface.Surface.Print(x + 0, y + 4, "/|   |\\", knightColor);
            _menuSurface.Surface.Print(x + 1, y + 5, "|   |", knightColor);
            _menuSurface.Surface.Print(x + 0, y + 6, "/ | | \\", knightColor);
            _menuSurface.Surface.Print(x + 1, y + 7, "|   |", knightColor);
            _menuSurface.Surface.Print(x + 0, y + 8, "[]   []", knightColor);
            _menuSurface.Surface.Print(x + 0, y + 9, "==|=====", swordColor);
            _menuSurface.Surface.Print(x + 2, y + 10, "|", swordColor);
            _menuSurface.Surface.Print(x + 0, y + 11, "==|=====", swordColor);
        }

        private void DrawRightKnight(int x, int y)
        {
            var knightColor = new Color(180, 180, 200);
            var swordColor = new Color(200, 200, 220);

            // Simplified knight using basic ASCII (mirrored)
            _menuSurface.Surface.Print(x + 3, y + 0, "[O]", knightColor);
            _menuSurface.Surface.Print(x + 2, y + 1, "/[#]\\", knightColor);
            _menuSurface.Surface.Print(x + 1, y + 2, "/ |#| \\", knightColor);
            _menuSurface.Surface.Print(x + 2, y + 3, "|   |", knightColor);
            _menuSurface.Surface.Print(x + 1, y + 4, "/|   |\\", knightColor);
            _menuSurface.Surface.Print(x + 2, y + 5, "|   |", knightColor);
            _menuSurface.Surface.Print(x + 1, y + 6, "/ | | \\", knightColor);
            _menuSurface.Surface.Print(x + 2, y + 7, "|   |", knightColor);
            _menuSurface.Surface.Print(x + 1, y + 8, "[]   []", knightColor);
            _menuSurface.Surface.Print(x + 0, y + 9, "=====|==", swordColor);
            _menuSurface.Surface.Print(x + 5, y + 10, "|", swordColor);
            _menuSurface.Surface.Print(x + 0, y + 11, "=====|==", swordColor);
        }

        private void DrawTitle(int centerX, int startY)
        {
            var titleColor = Color.Gold;

            // Large simplified title
            string[] titleLines = new[]
            {
                "##   ## ##  ## ##   ##  ###  ##   ##",
                "##   ## ##  ## ### ### ## ## ### ###",
                "####### ##  ## ####### ##### #######",
                "##   ## ##  ## ## # ## ## ## ## # ##",
                "##   ##  ####  ##   ## ## ## ##   ##",
                "",
                "##### ####  ##### ##### ##### ##### ##### #####",
                "##    ## ## ## ## ## ## ##    ##    ## ## ##",
                "##### ## ## ##### ##### ##### ##### ##### #####",
                "##    ## ## ## ## ##    ##    ##    ##    ##",
                "##    ####  ## ## ##    ##    ## ## ##### #####"
            };

            int titleWidth = titleLines[0].Length;
            int titleX = centerX - titleWidth / 2;

            for (int i = 0; i < titleLines.Length; i++)
            {
                _menuSurface.Surface.Print(titleX, startY + i, titleLines[i], titleColor);
            }
        }

        private void DrawMenuItems(int centerX, int startY)
        {
            DrawMenuItem(MenuItem.NewWorld, "NEW WORLD", centerX, startY);
            DrawMenuItem(MenuItem.LoadWorld, "LOAD WORLD", centerX, startY + MENU_ITEM_HEIGHT);
            DrawMenuItem(MenuItem.Settings, "SETTINGS", centerX, startY + MENU_ITEM_HEIGHT * 2);
            DrawMenuItem(MenuItem.Credits, "CREDITS", centerX, startY + MENU_ITEM_HEIGHT * 3);
            DrawMenuItem(MenuItem.Exit, "EXIT", centerX, startY + MENU_ITEM_HEIGHT * 4);
        }

        private void DrawMenuItem(MenuItem item, string text, int centerX, int y)
        {
            bool isSelected = _selectedItem == item;
            bool isHovered = _hoveredItem == item;
            bool isActive = isSelected || isHovered;

            int boxWidth = MENU_WIDTH;
            int boxX = centerX - boxWidth / 2;

            // Draw box background
            var bgColor = isActive ? new Color(60, 60, 40) : new Color(20, 20, 20);
            var borderColor = isActive ? Color.Gold : new Color(80, 80, 80);
            var textColor = isActive ? Color.White : new Color(150, 150, 150);

            // Fill background
            for (int x = boxX; x < boxX + boxWidth; x++)
            {
                _menuSurface.Surface.SetGlyph(x, y, ' ', Color.White, bgColor);
            }

            // Draw simple border
            for (int x = boxX; x < boxX + boxWidth; x++)
            {
                _menuSurface.Surface.SetGlyph(x, y, '-', borderColor);
            }

            // Draw arrow if selected
            if (isSelected)
            {
                _menuSurface.Surface.Print(boxX + 2, y, ">", Color.Gold);
            }

            // Draw text centered
            int textX = centerX - text.Length / 2;
            _menuSurface.Surface.Print(textX, y, text, textColor);
        }

        private MenuItem? GetMenuItemAtPosition(Point mousePos)
        {
            if (_currentPage != PageMode.MainMenu)
                return null;

            int centerX = _menuSurface.Surface.Width / 2;
            int boxWidth = MENU_WIDTH;
            int boxX = centerX - boxWidth / 2;

            for (int i = 0; i < 5; i++)
            {
                int itemY = MENU_START_Y + i * MENU_ITEM_HEIGHT;
                // Check if mouse is within the button area (including height)
                if (mousePos.X >= boxX && mousePos.X < boxX + boxWidth &&
                    mousePos.Y >= itemY && mousePos.Y < itemY + MENU_ITEM_HEIGHT)
                {
                    return (MenuItem)i;
                }
            }

            return null;
        }

        private void ExecuteMenuItem(MenuItem item)
        {
            switch (item)
            {
                case MenuItem.NewWorld:
                    _navigator.ShowWorldGeneration();
                    break;

                case MenuItem.LoadWorld:
                    _uiStore.AddToast("WIP: Load World feature coming soon!", _tick + 180);
                    break;

                case MenuItem.Settings:
                    _currentPage = PageMode.Settings;
                    DrawMenu();
                    break;

                case MenuItem.Credits:
                    _currentPage = PageMode.Credits;
                    DrawMenu();
                    break;

                case MenuItem.Exit:
                    Environment.Exit(0);
                    break;
            }
        }

        private void DrawToasts()
        {
            _uiStore.PruneToasts(_tick);

            int y = 2;
            foreach (var (text, _) in _uiStore.Toasts)
            {
                int x = (_menuSurface.Surface.Width - text.Length - 4) / 2;

                // Draw toast background
                for (int i = 0; i < text.Length + 4; i++)
                {
                    _menuSurface.Surface.SetGlyph(x + i, y, ' ', Color.White, new Color(40, 40, 40));
                }

                _menuSurface.Surface.Print(x + 2, y, text, Color.Yellow);
                y += 2;

                if (y > 10) break;
            }
        }

        public override bool ProcessKeyboard(Keyboard keyboard)
        {
            // ESC handling for pages
            if (keyboard.IsKeyPressed(Keys.Escape))
            {
                if (_currentPage != PageMode.MainMenu)
                {
                    _currentPage = PageMode.MainMenu;
                    DrawMenu();
                    return true;
                }
                else
                {
                    Environment.Exit(0);
                    return true;
                }
            }

            // Only handle navigation on main menu
            if (_currentPage != PageMode.MainMenu)
                return false;

            if (keyboard.IsKeyPressed(Keys.Up))
            {
                _selectedItem = (MenuItem)(((int)_selectedItem - 1 + 5) % 5);
                DrawMenu();
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.Down))
            {
                _selectedItem = (MenuItem)(((int)_selectedItem + 1) % 5);
                DrawMenu();
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.Enter) || keyboard.IsKeyPressed(Keys.Space))
            {
                ExecuteMenuItem(_selectedItem);
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.Q))
            {
                Environment.Exit(0);
                return true;
            }

            // Legacy keys for backwards compatibility
            if (keyboard.IsKeyPressed(Keys.N))
            {
                ExecuteMenuItem(MenuItem.NewWorld);
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.L))
            {
                ExecuteMenuItem(MenuItem.LoadWorld);
                return true;
            }

            return false;
        }
    }
}
