using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class MainMenuState
{
    private void DrawToasts()
    {
        _uiStore.PruneToasts(_tick);

        int y = 2;
        foreach (var (text, _) in _uiStore.Toasts)
        {
            int x = (_menuSurface.Surface.Width - text.Length - 4) / 2;

            for (int i = 0; i < text.Length + 4; i++)
            {
                _menuSurface.Surface.SetGlyph(x + i, y, ' ', Color.White, new Color(40, 40, 40));
            }

            _menuSurface.Surface.Print(x + 2, y, text, Color.Yellow);
            y += 2;

            if (y > 10) break;
        }
    }
}
