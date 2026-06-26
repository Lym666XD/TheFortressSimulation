using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class MainMenuState
{
    private void DrawTitle(int centerX, int startY)
    {
        var titleColor = Color.Gold;
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
}
