using HumanFortress.App.Diagnostics;
using HumanFortress.App.Runtime;
using HumanFortress.Runtime;

namespace HumanFortress.App.GameStates;

internal sealed class GameStateRuntimeCoordinator : IFortressPlayRuntimeHost
{
    private readonly IFortressRuntimeSessionPorts _runtimeSession;

    internal GameStateRuntimeCoordinator(
        bool strictContent,
        bool contentWarningsAsErrors)
    {
        var baseDir = AppContext.BaseDirectory;
        _runtimeSession = FortressRuntimeSessionFactory.Create(
            baseDir,
            strictContent,
            contentWarningsAsErrors,
            Logger.Log,
            category => Logger.CreateCallback(category),
            FortressContentIssueLogger.LogIssues);
    }

    internal IFortressRuntimeSessionAccess CreateRuntimeAccess()
    {
        return new FortressRuntimeAccess(_runtimeSession);
    }

    internal void InitializeWorld(int sizeInChunks, int maxZ)
    {
        _runtimeSession.InitializeWorld(sizeInChunks, maxZ);
    }

    internal void StartFortressPlay(bool enqueueAutoDig)
    {
        _runtimeSession.StartFortressPlay(enqueueAutoDig);
    }

    internal bool StopIfRunning()
    {
        return _runtimeSession.StopIfRunning();
    }

    IFortressRuntimeSessionAccess IFortressPlayRuntimeHost.CreateRuntimeAccess() => CreateRuntimeAccess();

    void IFortressPlayRuntimeHost.InitializeWorld(int sizeInChunks, int maxZ) => InitializeWorld(sizeInChunks, maxZ);
}
