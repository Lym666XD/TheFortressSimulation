using SadConsole;

namespace HumanFortress.App.GameStates;

internal abstract class ScreenGameState<TScreen> : GameState
    where TScreen : ScreenObject
{
    private readonly IGameScreenPresenter _screenPresenter;
    private TScreen? _screen;

    protected ScreenGameState(IGameScreenPresenter screenPresenter)
    {
        _screenPresenter = screenPresenter ?? throw new ArgumentNullException(nameof(screenPresenter));
    }

    protected virtual string ScreenOwnerName => Type.ToString();

    internal sealed override void Enter()
    {
        _screen = CreateScreen();
        _screenPresenter.TryShow(_screen, ScreenOwnerName);
    }

    internal override void Exit()
    {
        _screen = null;
    }

    protected abstract TScreen CreateScreen();
}
