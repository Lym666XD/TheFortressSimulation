using HumanFortress.App.GameStates;

namespace HumanFortress.App.Runtime;

public sealed class AppStateNavigator : IAppStateNavigator
{
    private readonly GameStateManager _stateManager;

    public AppStateNavigator(GameStateManager stateManager)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
    }

    public void ShowMainMenu()
    {
        _stateManager.ChangeState(GameStateType.MainMenu);
    }

    public void ShowWorldGeneration()
    {
        _stateManager.ChangeState(GameStateType.WorldGen);
    }

    public void ShowWorldMap()
    {
        _stateManager.ChangeState(GameStateType.WorldMap);
    }

    public void ShowEmbarkPreparation()
    {
        _stateManager.ChangeState(GameStateType.EmbarkPrep);
    }

    public void ShowFortressPlay()
    {
        _stateManager.ChangeState(GameStateType.FortressPlay);
    }
}
