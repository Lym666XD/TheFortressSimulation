namespace HumanFortress.App.GameStates;

internal sealed class GameStateRegistry
{
    private readonly Dictionary<GameStateType, GameState> _states = new();

    internal string RegisteredStateNames => string.Join(", ", _states.Keys);

    internal void Register(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _states[state.Type] = state;
    }

    internal GameState GetRequired(GameStateType stateType)
    {
        if (_states.TryGetValue(stateType, out var state))
        {
            return state;
        }

        Logger.Log($"[GameStateManager] ERROR: State {stateType} not found in registered states");
        throw new InvalidOperationException($"State {stateType} not registered");
    }
}
