namespace HumanFortress.App.Runtime;

public interface IAppStateNavigator
{
    void ShowMainMenu();
    void ShowWorldGeneration();
    void ShowWorldMap();
    void ShowEmbarkPreparation();
    void ShowFortressPlay();
}
