using HumanFortress.App;

namespace HumanFortress.App.GameStates;

internal sealed partial class GameStateManager
{
    private void ChangeState(GameStateType newStateType)
    {
        try
        {
            Logger.Log($"[GameStateManager] ChangeState from {_currentState?.Type} to {newStateType}");
            Logger.Log($"[GameStateManager] States registered: {_states.RegisteredStateNames}");

            ExitCurrentState();
            EnterState(newStateType);
        }
        catch (Exception ex)
        {
            LogTransitionFailure(ex);
            throw;
        }
    }

    private void ExitCurrentState()
    {
        if (_currentState == null)
            return;

        Logger.Log($"[GameStateManager] Calling Exit on {_currentState.Type}");
        _currentState.Exit();
        _runtimeLifecycle.AfterExit(_currentState.Type);
    }

    private void EnterState(GameStateType newStateType)
    {
        var newState = _states.GetRequired(newStateType);
        _currentState = newState;
        Logger.Log($"[GameStateManager] Calling Enter on {newStateType}");
        _currentState.Enter();
        Logger.Log($"[GameStateManager] Enter completed for {newStateType}");

        _runtimeLifecycle.AfterEnter(newStateType);
    }

    private static void LogTransitionFailure(Exception ex)
    {
        Logger.Log($"[GameStateManager] FATAL ERROR in ChangeState: {ex.Message}");
        Logger.Log($"[GameStateManager] Stack trace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Logger.Log($"[GameStateManager] Inner exception: {ex.InnerException.Message}");
            Logger.Log($"[GameStateManager] Inner stack: {ex.InnerException.StackTrace}");
        }
    }
}
