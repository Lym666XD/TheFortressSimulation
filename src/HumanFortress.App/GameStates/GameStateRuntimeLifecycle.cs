namespace HumanFortress.App.GameStates;

internal sealed class GameStateRuntimeLifecycle
{
    private readonly GameStateRuntimeCoordinator _runtimeCoordinator;
    private readonly bool _enqueueAutoDig;

    internal GameStateRuntimeLifecycle(GameStateRuntimeCoordinator runtimeCoordinator, bool enqueueAutoDig)
    {
        _runtimeCoordinator = runtimeCoordinator ?? throw new ArgumentNullException(nameof(runtimeCoordinator));
        _enqueueAutoDig = enqueueAutoDig;
    }

    internal void AfterExit(GameStateType exitedState)
    {
        if (exitedState != GameStateType.FortressPlay)
        {
            return;
        }

        Logger.Log("[GameStateManager] Stopping simulation");
        _runtimeCoordinator.StopIfRunning();
    }

    internal void AfterEnter(GameStateType enteredState)
    {
        if (enteredState != GameStateType.FortressPlay)
        {
            return;
        }

        Logger.Log("[GameStateManager] Starting simulation for FortressPlay");
        _runtimeCoordinator.StartFortressPlay(_enqueueAutoDig);
    }

    internal void Shutdown()
    {
        Logger.Log("[GameStateManager] Disposing runtime session");
        _runtimeCoordinator.Dispose();
    }
}
