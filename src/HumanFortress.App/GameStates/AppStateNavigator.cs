namespace HumanFortress.App.GameStates;

internal sealed class AppStateNavigator : IAppStateNavigator
{
    private readonly GameStateManager _stateManager;

    internal AppStateNavigator(GameStateManager stateManager)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
    }

    internal void ShowMainMenu()
    {
        _stateManager.TransitionTo(GameStateType.MainMenu);
    }

    internal void ShowWorldGeneration()
    {
        _stateManager.TransitionTo(GameStateType.WorldGen);
    }

    internal void ShowWorldMap()
    {
        _stateManager.TransitionTo(GameStateType.WorldMap);
    }

    internal void ShowEmbarkPreparation()
    {
        _stateManager.TransitionTo(GameStateType.EmbarkPrep);
    }

    internal void ShowFortressPlay()
    {
        _stateManager.TransitionTo(GameStateType.FortressPlay);
    }

    void IAppStateNavigator.ShowMainMenu() => ShowMainMenu();

    void IAppStateNavigator.ShowWorldGeneration() => ShowWorldGeneration();

    void IAppStateNavigator.ShowWorldMap() => ShowWorldMap();

    void IAppStateNavigator.ShowEmbarkPreparation() => ShowEmbarkPreparation();

    void IAppStateNavigator.ShowFortressPlay() => ShowFortressPlay();
}
