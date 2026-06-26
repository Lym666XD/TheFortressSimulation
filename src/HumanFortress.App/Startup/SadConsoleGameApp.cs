using HumanFortress.App.GameStates;
using HumanFortress.App.Session;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Startup;

internal sealed class SadConsoleGameApp
{
    private readonly AppStartupOptions _options;
    private GameStateManager? _gameStateManager;

    public SadConsoleGameApp(AppStartupOptions options)
    {
        _options = options;
    }

    public void OnStarted(object? sender, GameHost gameHost)
    {
        var session = new FortressSessionContext(_options.AutoDig);
        _gameStateManager = new GameStateManager(
            enqueueAutoDig: _options.AutoDig,
            strictContent: _options.StrictContent,
            contentWarningsAsErrors: _options.ContentWarningsAsErrors);
        var navigator = new AppStateNavigator(_gameStateManager);

        AppStateRegistration.RegisterAll(_gameStateManager, navigator, session);

        if (_options.AutoDig)
        {
            session.ConfigureEmbark(new Point(10, 10), 2);
            _gameStateManager.TransitionTo(GameStateType.FortressPlay);
            return;
        }

        _gameStateManager.TransitionTo(GameStateType.MainMenu);
    }

    public void Shutdown()
    {
        Logger.Log("[SHUTDOWN] Game window closed, shutting down systems");
        _gameStateManager?.Shutdown();
    }
}
