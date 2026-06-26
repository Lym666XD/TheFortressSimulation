using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class WorldGenState
{
    private void DrawUI()
    {
        _menuSurface.Surface.Clear();

        int centerX = _menuSurface.Surface.Width / 2;

        _menuSurface.Surface.Print(centerX - 12, 3, "=== WORLD GENERATION ===", Color.Gold);

        DrawBorder(centerX - 28, 8, 56, 11, Color.DarkCyan);

        _menuSurface.Surface.Print(centerX - 26, 9, "World Parameters:", Color.Cyan);

        DrawParameterField(UIElement.Name, centerX, 10, "World Name:", _settings.Name, true);
        DrawParameterField(UIElement.Seed, centerX, 12, "Seed:", _settings.Seed.ToString(), false);
        DrawParameterField(UIElement.Size, centerX, 14, "World Size:", $"{_settings.Width}x{_settings.Height}", false, true);
        DrawParameterField(UIElement.Difficulty, centerX, 16, "Difficulty:", _settings.Difficulty.ToString(), false, true);

        _menuSurface.Surface.Print(centerX - 26, 19, "Quick Start Presets:", Color.Cyan);

        DrawButton(UIElement.PresetBeginner, centerX - 30, 20, "Beginner", 10);
        DrawButton(UIElement.PresetStandard, centerX - 10, 20, "Standard", 12);
        DrawButton(UIElement.PresetChallenge, centerX + 12, 20, "Challenge", 12);

        DrawButton(UIElement.ButtonGenerate, centerX - 30, 24, "Generate World", 18, Color.Green);
        DrawButton(UIElement.ButtonRandomAll, centerX - 8, 24, "Random All", 16, Color.Yellow);
        DrawButton(UIElement.ButtonBack, centerX + 12, 24, "Back", 8, Color.Red);

        _menuSurface.Surface.Print(centerX - 35, _menuSurface.Surface.Height - 4,
            "Controls: Up/Down Select | Left/Right Modify | Enter Confirm | R Random Seed | Right-Click/ESC Back",
            Color.DarkGray);

        _menuSurface.Surface.IsDirty = true;
    }
}
