using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class MainMenuState
{
    private void DrawMainMenu()
    {
        int centerX = _menuSurface.Surface.Width / 2;
        int centerY = _menuSurface.Surface.Height / 2;

        DrawLeftKnight(5, centerY - 6);
        DrawRightKnight(_menuSurface.Surface.Width - 15, centerY - 6);
        DrawTitle(centerX, 3);
        DrawMenuItems(centerX, MENU_START_Y);

        bool blink = (_tick / 30) % 2 == 0;
        var hintColor = blink ? Color.Cyan : Color.DarkCyan;
        _menuSurface.Surface.Print(centerX - 25, _menuSurface.Surface.Height - 3, "Use Arrow Keys or Mouse to select | Enter to confirm", hintColor);

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
}
