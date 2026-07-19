using HumanFortress.Contracts.Time;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Runtime.Startup;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    void IDisposable.Dispose()
    {
        _lifecycle.Dispose();
    }

    void IFortressRuntimeSessionLifecyclePort.InitializeWorld(int sizeInChunks, int maxZ)
    {
        InitializeWorldCore(sizeInChunks, maxZ);
    }

    TickSchedulerStopResult IFortressRuntimeSessionLifecyclePort.Stop(TimeSpan timeout)
    {
        return StopRuntimeCore(timeout);
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
        _lifecycle.InitializeWorld(
            sizeInChunks,
            maxZ,
            ActivateCheckpointGeneration);
    }

    private bool StopIfRunningCore()
    {
        return _lifecycle.StopIfRunning();
    }

    private TickSchedulerStopResult StopRuntimeCore(TimeSpan timeout)
    {
        return _lifecycle.Stop(timeout);
    }

    private void StartFortressPlayCore(bool enqueueAutoDig)
    {
        _lifecycle.Start((runtime, services) =>
            FortressRuntimeStartup.Start(
                runtime,
                enqueueAutoDig,
                services.CommandQueue,
                services.TickScheduler,
                (world, queue, tick) => RuntimeAutoDigSeeder.EnqueueIfPossible(world, queue, tick, _log),
                _log));
    }

    private World? World => _runtimeSession?.World;

    private void InvalidateFrameSnapshots()
    {
        _frameSnapshots.Invalidate();
    }

    private SimulationRuntimeHost<SimulationRuntimeSystems>? RuntimeHost => _runtimeSession?.Host;

    private SimulationRuntimeHost<SimulationRuntimeSystems> RequireRuntimeHost()
    {
        if (_runtimeSession == null)
            throw new InvalidOperationException("World not initialized");

        return _runtimeSession.Host;
    }
}
