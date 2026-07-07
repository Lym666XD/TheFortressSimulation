namespace HumanFortress.App.GameStates;

internal interface IAppStateNavigator
{
    void ShowMainMenu();
    void ShowWorldGeneration();
    void ShowWorldMap();
    void ShowEmbarkPreparation();
    void ShowFortressPlay();
}
