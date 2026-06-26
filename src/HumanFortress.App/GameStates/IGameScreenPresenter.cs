using SadConsole;

namespace HumanFortress.App.GameStates;

internal interface IGameScreenPresenter
{
    bool TryShow(ScreenObject screen, string ownerName);
}

internal sealed class SadConsoleGameScreenPresenter : IGameScreenPresenter
{
    internal bool TryShow(ScreenObject screen, string ownerName)
    {
        ArgumentNullException.ThrowIfNull(screen);

        screen.IsFocused = true;
        var gameHost = GameHost.Instance;
        if (gameHost is null)
        {
            Logger.Log($"[{ownerName}] GameHost not initialized, deferring screen setup");
            return false;
        }

        gameHost.Screen = screen;
        gameHost.Screen.IsFocused = true;
        return true;
    }

    bool IGameScreenPresenter.TryShow(ScreenObject screen, string ownerName) => TryShow(screen, ownerName);
}
