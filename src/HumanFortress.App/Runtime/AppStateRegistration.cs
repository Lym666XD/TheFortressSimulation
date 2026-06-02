using HumanFortress.App.GameStates;
using HumanFortress.App.States;
using SadConsole;

namespace HumanFortress.App.Runtime;

internal static class AppStateRegistration
{
    public static void RegisterAll(GameStateManager stateManager, IAppStateNavigator navigator, FortressSessionContext session)
    {
        ArgumentNullException.ThrowIfNull(stateManager);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(session);

        stateManager.RegisterState(new MainMenuGameState(navigator));
        stateManager.RegisterState(new WorldGenGameState(navigator, session));
        stateManager.RegisterState(new WorldMapGameState(navigator, session));
        stateManager.RegisterState(new EmbarkPrepGameState(navigator, session));
        stateManager.RegisterState(new FortressPlayGameState(stateManager, session));
    }
}

internal sealed class MainMenuGameState : GameState
{
    private readonly IAppStateNavigator _navigator;
    private MainMenuState? _mainMenuState;

    public MainMenuGameState(IAppStateNavigator navigator)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
    }

    public override GameStateType Type => GameStateType.MainMenu;

    public override void Enter()
    {
        Logger.Log("Entered Main Menu");
        _mainMenuState = new MainMenuState(_navigator);
        _mainMenuState.IsFocused = true;
        GameHost.Instance.Screen = _mainMenuState;
        GameHost.Instance.Screen.IsFocused = true;
    }

    public override void Exit()
    {
        _mainMenuState = null;
    }
}

internal sealed class FortressPlayGameState : GameState
{
    private readonly GameStateManager _gameStateManager;
    private readonly FortressSessionContext _session;
    private FortressState? _fortressState;

    public FortressPlayGameState(GameStateManager gameStateManager, FortressSessionContext session)
    {
        _gameStateManager = gameStateManager ?? throw new ArgumentNullException(nameof(gameStateManager));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public override GameStateType Type => GameStateType.FortressPlay;

    public override void Enter()
    {
        Logger.Log("Entered Fortress Play");

        int fortressSize = _session.FortressSize;
        if (fortressSize < 2 || fortressSize > 8)
        {
            Logger.Log($"[FortressPlayState] Invalid fortress size {fortressSize}, defaulting to 2");
            fortressSize = 2;
        }

        _gameStateManager.InitializeWorld(fortressSize, 50);
        _fortressState = new FortressState(new FortressRuntimeAccess(_gameStateManager), _session);

        if (GameHost.Instance != null)
        {
            _fortressState.IsFocused = true;
            GameHost.Instance.Screen = _fortressState;
            GameHost.Instance.Screen.IsFocused = true;
        }
        else
        {
            Logger.Log("[FortressPlayState] GameHost not initialized, deferring screen setup");
        }
    }

    public override void Exit()
    {
        _fortressState = null;
    }
}

internal sealed class WorldGenGameState : GameState
{
    private readonly IAppStateNavigator _navigator;
    private readonly FortressSessionContext _session;
    private WorldGenState? _worldGenState;

    public WorldGenGameState(IAppStateNavigator navigator, FortressSessionContext session)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public override GameStateType Type => GameStateType.WorldGen;

    public override void Enter()
    {
        _worldGenState = new WorldGenState(_navigator, _session);
        _worldGenState.IsFocused = true;
        GameHost.Instance.Screen = _worldGenState;
        GameHost.Instance.Screen.IsFocused = true;
    }

    public override void Exit()
    {
        _worldGenState = null;
    }
}

internal sealed class WorldMapGameState : GameState
{
    private readonly IAppStateNavigator _navigator;
    private readonly FortressSessionContext _session;
    private WorldMapState? _worldMapState;

    public WorldMapGameState(IAppStateNavigator navigator, FortressSessionContext session)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public override GameStateType Type => GameStateType.WorldMap;

    public override void Enter()
    {
        _worldMapState = new WorldMapState(_navigator, _session);
        _worldMapState.IsFocused = true;
        GameHost.Instance.Screen = _worldMapState;
        GameHost.Instance.Screen.IsFocused = true;
    }

    public override void Exit()
    {
        _worldMapState = null;
    }
}

internal sealed class EmbarkPrepGameState : GameState
{
    private readonly IAppStateNavigator _navigator;
    private readonly FortressSessionContext _session;
    private EmbarkPrepState? _embarkPrepState;

    public EmbarkPrepGameState(IAppStateNavigator navigator, FortressSessionContext session)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public override GameStateType Type => GameStateType.EmbarkPrep;

    public override void Enter()
    {
        _embarkPrepState = new EmbarkPrepState(_navigator, _session);
        _embarkPrepState.IsFocused = true;
        GameHost.Instance.Screen = _embarkPrepState;
        GameHost.Instance.Screen.IsFocused = true;
    }

    public override void Exit()
    {
        _embarkPrepState = null;
    }
}
