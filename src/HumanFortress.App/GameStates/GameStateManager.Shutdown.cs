using HumanFortress.App;

namespace HumanFortress.App.GameStates;

internal sealed partial class GameStateManager
{
    /// <summary>
    /// Shutdown and cleanup all systems before application exit.
    /// </summary>
    internal void Shutdown()
    {
        Logger.Log("[GameStateManager] Shutdown requested");

        _runtimeLifecycle.Shutdown();
        ExitStateDuringShutdown();

        Logger.Log("[GameStateManager] Shutdown complete");
    }

    private void ExitStateDuringShutdown()
    {
        if (_currentState == null)
            return;

        Logger.Log($"[GameStateManager] Exiting current state: {_currentState.Type}");
        _currentState.Exit();
    }
}
