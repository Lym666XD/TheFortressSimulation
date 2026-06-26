using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class WorldGenState
{
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
}
