using HumanFortress.App.Session;

namespace HumanFortress.App.GameStates;

internal static class AppStateRegistration
{
    internal static void RegisterAll(GameStateManager stateManager, IAppStateNavigator navigator, FortressSessionContext session)
    {
        ArgumentNullException.ThrowIfNull(stateManager);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(session);

        var screenPresenter = new SadConsoleGameScreenPresenter();

        stateManager.RegisterState(new MainMenuGameState(navigator, screenPresenter));
        stateManager.RegisterState(new WorldGenGameState(navigator, session, screenPresenter));
        stateManager.RegisterState(new WorldMapGameState(navigator, session, screenPresenter));
        stateManager.RegisterState(new EmbarkPrepGameState(navigator, session, screenPresenter));
        stateManager.RegisterState(new FortressPlayGameState(stateManager.FortressPlayRuntimeHost, session, screenPresenter));
    }
}
