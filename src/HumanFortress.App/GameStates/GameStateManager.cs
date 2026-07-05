using HumanFortress.App;

namespace HumanFortress.App.GameStates;

/// <summary>
/// Manages game state registration and transitions per GAME_ARCHITECTURE.md.
/// </summary>
internal sealed partial class GameStateManager
{
    private readonly GameStateRegistry _states;
    private readonly GameStateRuntimeCoordinator _runtimeCoordinator;
    private readonly GameStateRuntimeLifecycle _runtimeLifecycle;

    private GameState? _currentState;

    internal GameStateManager(
        bool enqueueAutoDig = false,
        bool strictContent = false,
        bool contentWarningsAsErrors = false)
    {
        _states = new GameStateRegistry();
        _runtimeCoordinator = new GameStateRuntimeCoordinator(
            GameStateRuntimeConfiguration.CreateDefault(
                strictContent,
                contentWarningsAsErrors));
        _runtimeLifecycle = new GameStateRuntimeLifecycle(_runtimeCoordinator, enqueueAutoDig);
    }

    internal IFortressPlayRuntimeHost FortressPlayRuntimeHost => _runtimeCoordinator;

    /// <summary>
    /// Register a state.
    /// </summary>
    internal void RegisterState(GameState state)
    {
        _states.Register(state);
    }

    /// <summary>
    /// Transition to a new state.
    /// </summary>
    internal void TransitionTo(GameStateType newStateType)
    {
        ChangeState(newStateType);
    }

}
