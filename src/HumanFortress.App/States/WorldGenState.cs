using System;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.Core.World;
using HumanFortress.WorldGen;
using HumanFortress.App.GameStates;
using HumanFortress.App.UI;

namespace HumanFortress.App.States
{
    public class WorldGenState : ScreenObject
    {
        private readonly ScreenSurface _surface;
        private readonly MenuSurface _menuSurface;
        private readonly SadConsole.Console _progressConsole;
        private WorldParams _params;
        private WorldGenerator _generator;
        private bool _isGenerating;
        private WorldGenResult _result;

        // UI State
        private enum UIElement
        {
            Name = 0,
            Seed = 1,
            Size = 2,
            Difficulty = 3,
            PresetBeginner = 4,
            PresetStandard = 5,
            PresetChallenge = 6,
            ButtonGenerate = 7,
            ButtonRandomAll = 8,
            ButtonBack = 9
        }

        private UIElement _selectedElement = UIElement.Name;
        private UIElement? _hoveredElement = null;
        private bool _isEditingName = false;
        private string _nameBuffer = "";

        public WorldGenState()
        {
            // Create main surface that fills the screen
            _surface = new ScreenSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            _surface.UseMouse = false;
            _surface.UseKeyboard = false;

            // Create menu surface with mouse support
            _menuSurface = new MenuSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            _menuSurface.Position = new Point(0, 0);
            _menuSurface.MouseMovedLocal += OnMouseMoved;
            _menuSurface.LeftClickedLocal += OnMouseClicked;
            _menuSurface.RightClickedLocal += OnRightClicked;

            _progressConsole = new SadConsole.Console(80, 10);
            _progressConsole.Position = new Point(20, 35);

            // Add consoles to the surface
            _surface.Children.Add(_menuSurface);
            _surface.Children.Add(_progressConsole);

            // Add the surface as the only child
            Children.Add(_surface);

            // Make this ScreenObject focusable
            IsFocused = true;
            UseKeyboard = true;
            UseMouse = false;

            _params = WorldParams.Default;
            _nameBuffer = _params.Name;
            _generator = new WorldGenerator();
            _generator.ProgressChanged += OnProgressChanged;

            DrawUI();
        }

        private void OnMouseMoved(Point local)
        {
            if (_isGenerating)
                return;

            var newHoveredElement = GetElementAtPosition(local);
            if (newHoveredElement != _hoveredElement)
            {
                _hoveredElement = newHoveredElement;
                DrawUI();
            }
        }

        private void OnMouseClicked(Point local)
        {
            if (_isGenerating)
                return;

            var clickedElement = GetElementAtPosition(local);
            if (clickedElement.HasValue)
            {
                HandleElementClick(clickedElement.Value);
            }
        }

        private void OnRightClicked(Point local)
        {
            if (_isGenerating)
                return;

            GameStateManager.Instance.ChangeState(GameStateType.MainMenu);
        }

        private UIElement? GetElementAtPosition(Point pos)
        {
            int centerX = _menuSurface.Surface.Width / 2;

            // Name field (y=10)
            if (pos.Y == 10 && pos.X >= centerX - 25 && pos.X < centerX + 25)
                return UIElement.Name;

            // Seed field (y=12)
            if (pos.Y == 12 && pos.X >= centerX - 25 && pos.X < centerX + 25)
                return UIElement.Seed;

            // Size field (y=14)
            if (pos.Y == 14 && pos.X >= centerX - 25 && pos.X < centerX + 25)
                return UIElement.Size;

            // Difficulty field (y=16)
            if (pos.Y == 16 && pos.X >= centerX - 25 && pos.X < centerX + 25)
                return UIElement.Difficulty;

            // Preset buttons (y=20)
            if (pos.Y >= 20 && pos.Y < 22)
            {
                if (pos.X >= centerX - 30 && pos.X < centerX - 20)
                    return UIElement.PresetBeginner;
                if (pos.X >= centerX - 10 && pos.X < centerX + 2)
                    return UIElement.PresetStandard;
                if (pos.X >= centerX + 12 && pos.X < centerX + 24)
                    return UIElement.PresetChallenge;
            }

            // Action buttons (y=24-25)
            if (pos.Y >= 24 && pos.Y < 26)
            {
                if (pos.X >= centerX - 30 && pos.X < centerX - 12)
                    return UIElement.ButtonGenerate;
                if (pos.X >= centerX - 8 && pos.X < centerX + 8)
                    return UIElement.ButtonRandomAll;
                if (pos.X >= centerX + 12 && pos.X < centerX + 20)
                    return UIElement.ButtonBack;
            }

            return null;
        }

        private void HandleElementClick(UIElement element)
        {
            switch (element)
            {
                case UIElement.Name:
                    _selectedElement = UIElement.Name;
                    _isEditingName = true;
                    _nameBuffer = _params.Name;
                    DrawUI();
                    break;

                case UIElement.Seed:
                case UIElement.Size:
                case UIElement.Difficulty:
                    _selectedElement = element;
                    _isEditingName = false;
                    DrawUI();
                    break;

                case UIElement.PresetBeginner:
                    ApplyPreset(DifficultyPreset.Easy, 128);
                    break;

                case UIElement.PresetStandard:
                    ApplyPreset(DifficultyPreset.Normal, 256);
                    break;

                case UIElement.PresetChallenge:
                    ApplyPreset(DifficultyPreset.Hard, 512);
                    break;

                case UIElement.ButtonGenerate:
                    StartGeneration();
                    break;

                case UIElement.ButtonRandomAll:
                    RandomizeAll();
                    break;

                case UIElement.ButtonBack:
                    GameStateManager.Instance.ChangeState(GameStateType.MainMenu);
                    break;
            }
        }

        private void DrawUI()
        {
            _menuSurface.Surface.Clear();

            int centerX = _menuSurface.Surface.Width / 2;

            // Title
            _menuSurface.Surface.Print(centerX - 12, 3, "=== WORLD GENERATION ===", Color.Gold);

            // Draw parameter section
            DrawBorder(centerX - 28, 8, 56, 11, Color.DarkCyan);

            _menuSurface.Surface.Print(centerX - 26, 9, "World Parameters:", Color.Cyan);

            // Name
            DrawParameterField(UIElement.Name, centerX, 10, "World Name:", _params.Name, true);

            // Seed
            DrawParameterField(UIElement.Seed, centerX, 12, "Seed:", _params.Seed.ToString(), false);

            // Size with arrows
            DrawParameterField(UIElement.Size, centerX, 14, "World Size:", $"{_params.Width}x{_params.Height}", false, true);

            // Difficulty with arrows
            DrawParameterField(UIElement.Difficulty, centerX, 16, "Difficulty:", _params.Difficulty.ToString(), false, true);

            // Quick Start Presets
            _menuSurface.Surface.Print(centerX - 26, 19, "Quick Start Presets:", Color.Cyan);

            DrawButton(UIElement.PresetBeginner, centerX - 30, 20, "Beginner", 10);
            DrawButton(UIElement.PresetStandard, centerX - 10, 20, "Standard", 12);
            DrawButton(UIElement.PresetChallenge, centerX + 12, 20, "Challenge", 12);

            // Action Buttons
            DrawButton(UIElement.ButtonGenerate, centerX - 30, 24, "Generate World", 18, Color.Green);
            DrawButton(UIElement.ButtonRandomAll, centerX - 8, 24, "Random All", 16, Color.Yellow);
            DrawButton(UIElement.ButtonBack, centerX + 12, 24, "Back", 8, Color.Red);

            // Controls hint
            _menuSurface.Surface.Print(centerX - 35, _menuSurface.Surface.Height - 4,
                "Controls: ↑↓ Select | ←→ Modify | Enter Confirm | R Random Seed | Right-Click/ESC Back",
                Color.DarkGray);

            _menuSurface.Surface.IsDirty = true;
        }

        private void DrawBorder(int x, int y, int width, int height, Color color)
        {
            // Top and bottom
            for (int i = 0; i < width; i++)
            {
                _menuSurface.Surface.SetGlyph(x + i, y, '-', color);
                _menuSurface.Surface.SetGlyph(x + i, y + height - 1, '-', color);
            }

            // Left and right
            for (int i = 0; i < height; i++)
            {
                _menuSurface.Surface.SetGlyph(x, y + i, '|', color);
                _menuSurface.Surface.SetGlyph(x + width - 1, y + i, '|', color);
            }

            // Corners
            _menuSurface.Surface.SetGlyph(x, y, '+', color);
            _menuSurface.Surface.SetGlyph(x + width - 1, y, '+', color);
            _menuSurface.Surface.SetGlyph(x, y + height - 1, '+', color);
            _menuSurface.Surface.SetGlyph(x + width - 1, y + height - 1, '+', color);
        }

        private void DrawParameterField(UIElement element, int centerX, int y, string label, string value, bool editable, bool hasArrows = false)
        {
            bool isSelected = _selectedElement == element;
            bool isHovered = _hoveredElement == element;
            bool isActive = isSelected || isHovered;
            bool isEditing = _isEditingName && element == UIElement.Name;

            var labelColor = Color.White;
            var valueColor = isActive ? Color.Yellow : Color.Gray;
            var bgColor = isActive ? new Color(40, 40, 30) : Color.Transparent;

            // Highlight background if active
            if (isActive)
            {
                for (int x = centerX - 25; x < centerX + 25; x++)
                {
                    _menuSurface.Surface.SetGlyph(x, y, ' ', Color.White, bgColor);
                }
            }

            // Draw label
            _menuSurface.Surface.Print(centerX - 24, y, label, labelColor);

            // Draw value with arrows if applicable
            string displayValue = value;
            if (isEditing)
            {
                displayValue = _nameBuffer + "_";
            }

            if (hasArrows && isActive)
            {
                _menuSurface.Surface.Print(centerX - 2, y, "<", Color.Cyan);
                _menuSurface.Surface.Print(centerX, y, displayValue, valueColor);
                _menuSurface.Surface.Print(centerX + displayValue.Length, y, ">", Color.Cyan);
            }
            else
            {
                _menuSurface.Surface.Print(centerX, y, displayValue, valueColor);
            }

            // Selection indicator
            if (isSelected && !isEditing)
            {
                _menuSurface.Surface.Print(centerX - 26, y, ">", Color.Gold);
            }
        }

        private void DrawButton(UIElement element, int x, int y, string text, int width, Color? customColor = null)
        {
            bool isHovered = _hoveredElement == element;
            bool isSelected = _selectedElement == element;
            bool isActive = isHovered || isSelected;

            var bgColor = isActive ? new Color(60, 60, 40) : new Color(20, 20, 20);
            var textColor = isActive ? Color.White : (customColor ?? Color.Gray);
            var borderColor = isActive ? (customColor ?? Color.Gold) : Color.DarkGray;

            // Background
            for (int i = 0; i < width; i++)
            {
                _menuSurface.Surface.SetGlyph(x + i, y, ' ', Color.White, bgColor);
                _menuSurface.Surface.SetGlyph(x + i, y + 1, ' ', Color.White, bgColor);
            }

            // Border
            for (int i = 0; i < width; i++)
            {
                _menuSurface.Surface.SetGlyph(x + i, y, '-', borderColor);
                _menuSurface.Surface.SetGlyph(x + i, y + 1, '-', borderColor);
            }

            // Text (centered)
            int textX = x + (width - text.Length) / 2;
            _menuSurface.Surface.Print(textX, y, text, textColor);
        }

        private void ApplyPreset(DifficultyPreset difficulty, int size)
        {
            _params.Difficulty = difficulty;
            _params.Width = size;
            _params.Height = size;
            _params.Seed = (uint)Environment.TickCount;
            _isEditingName = false;
            DrawUI();
        }

        private void RandomizeAll()
        {
            _params.Seed = (uint)Environment.TickCount;
            int[] sizes = { 128, 256, 512 };
            _params.Width = sizes[new Random().Next(sizes.Length)];
            _params.Height = _params.Width;
            _params.Difficulty = (DifficultyPreset)new Random().Next(4);
            DrawUI();
        }

        private void OnProgressChanged(string stage, float progress)
        {
            _progressConsole.Clear();
            _progressConsole.Print(0, 0, "Generation Progress:", Color.Cyan);
            _progressConsole.Print(0, 2, stage, Color.White);

            int barWidth = 70;
            int filled = (int)(barWidth * progress);
            string bar = "[" + new string('=', filled) + new string('-', barWidth - filled) + "]";
            _progressConsole.Print(0, 4, bar, Color.Green);
            _progressConsole.Print(0, 5, $"{(int)(progress * 100)}%", Color.White);
        }

        public override bool ProcessKeyboard(Keyboard keyboard)
        {
            if (_isGenerating)
                return true;

            // Handle name editing
            if (_isEditingName)
            {
                if (keyboard.IsKeyPressed(Keys.Escape))
                {
                    _isEditingName = false;
                    DrawUI();
                    return true;
                }
                else if (keyboard.IsKeyPressed(Keys.Enter))
                {
                    _params.Name = _nameBuffer;
                    _isEditingName = false;
                    DrawUI();
                    return true;
                }
                else if (keyboard.IsKeyPressed(Keys.Back))
                {
                    if (_nameBuffer.Length > 0)
                    {
                        _nameBuffer = _nameBuffer.Substring(0, _nameBuffer.Length - 1);
                        DrawUI();
                    }
                    return true;
                }
                else
                {
                    // Handle character input
                    foreach (var asciiKey in keyboard.KeysPressed)
                    {
                        char c = GetCharFromKey(asciiKey.Key);
                        if (c != '\0' && _nameBuffer.Length < 20)
                        {
                            _nameBuffer += c;
                            DrawUI();
                            break;
                        }
                    }
                    return true;
                }
            }

            // Normal navigation
            if (keyboard.IsKeyPressed(Keys.Escape))
            {
                GameStateManager.Instance.ChangeState(GameStateType.MainMenu);
                return true;
            }

            if (keyboard.IsKeyPressed(Keys.Up))
            {
                _selectedElement = (UIElement)(((int)_selectedElement - 1 + 10) % 10);
                DrawUI();
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.Down))
            {
                _selectedElement = (UIElement)(((int)_selectedElement + 1) % 10);
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
                HandleElementClick(_selectedElement);
                return true;
            }

            return false;
        }

        private char GetCharFromKey(Keys key)
        {
            // Basic character mapping
            if (key >= Keys.A && key <= Keys.Z)
            {
                return (char)('a' + (key - Keys.A));
            }
            else if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }
            else if (key == Keys.Space)
            {
                return ' ';
            }
            return '\0';
        }

        private void ModifySelectedOption(bool increase)
        {
            switch (_selectedElement)
            {
                case UIElement.Name:
                    _isEditingName = true;
                    _nameBuffer = _params.Name;
                    break;

                case UIElement.Seed:
                    if (increase)
                        _params.Seed++;
                    else
                        _params.Seed--;
                    break;

                case UIElement.Size:
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

                case UIElement.Difficulty:
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
                _progressConsole.Print(0, 7, _result.ErrorMessage, Color.Red);
                _isGenerating = false;
            }
        }
    }
}
