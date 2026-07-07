using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Runtime.Session;
using HumanFortress.Runtime.Startup;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    void IFortressRuntimeSessionLifecyclePort.InitializeWorld(int sizeInChunks, int maxZ)
    {
        InitializeWorldCore(sizeInChunks, maxZ);
    }

    bool IFortressRuntimeSessionLifecyclePort.StopIfRunning()
    {
        return StopIfRunningCore();
    }

    void IFortressRuntimeSessionLifecyclePort.StartFortressPlay(bool enqueueAutoDig)
    {
        StartFortressPlayCore(enqueueAutoDig);
    }

    private void InitializeWorldCore(int sizeInChunks, int maxZ)
    {
        StopIfRunningCore();
        _workshopCompletionNotifier.SetHandler(null);
        _runtimeSession = new FortressRuntimeSession(_runtimeSessionFactory.CreateNew(sizeInChunks, maxZ));
    }

    private bool StopIfRunningCore()
    {
        var runtime = RuntimeHost;
        if (runtime?.IsRunning != true)
        {
            _workshopCompletionNotifier.SetHandler(null);
            return false;
        }

        runtime.Stop();
        _workshopCompletionNotifier.SetHandler(null);
        return true;
    }

    private void StartFortressPlayCore(bool enqueueAutoDig)
    {
        var runtime = RequireRuntimeHost();
        FortressRuntimeStartup.Start(
            runtime,
            enqueueAutoDig,
            _services.CommandQueue,
            _services.TickScheduler,
            (world, queue, tick) => RuntimeAutoDigSeeder.EnqueueIfPossible(world, queue, tick, _log),
            _log);
    }

    private World? World => _runtimeSession?.World;

    private SimulationRuntimeHost<SimulationRuntimeSystems>? RuntimeHost => _runtimeSession?.Host;

    private SimulationRuntimeHost<SimulationRuntimeSystems> RequireRuntimeHost()
    {
        if (_runtimeSession == null)
            throw new InvalidOperationException("World not initialized");

        return _runtimeSession.Host;
    }
}
