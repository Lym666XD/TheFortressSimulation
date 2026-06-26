using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class MainMenuState
{
    private void DrawLeftKnight(int x, int y)
    {
        var knightColor = new Color(180, 180, 200);
        var swordColor = new Color(200, 200, 220);

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
}
