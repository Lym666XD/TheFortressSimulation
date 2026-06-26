using HumanFortress.App.States;

namespace HumanFortress.App.GameStates;

internal sealed class MainMenuGameState : ScreenGameState<MainMenuState>
{
    private readonly IAppStateNavigator _navigator;

    internal MainMenuGameState(IAppStateNavigator navigator, IGameScreenPresenter screenPresenter)
        : base(screenPresenter)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
    }

    internal override GameStateType Type => GameStateType.MainMenu;

    protected override MainMenuState CreateScreen()
    {
        Logger.Log("Entered Main Menu");
        return new MainMenuState(_navigator);
    }
}
