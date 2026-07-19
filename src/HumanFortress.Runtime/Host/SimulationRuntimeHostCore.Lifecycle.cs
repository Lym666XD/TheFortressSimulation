using HumanFortress.Contracts.Time;
using HumanFortress.Core.Time;

namespace HumanFortress.Runtime.Host;

internal sealed partial class SimulationRuntimeHostCore
{
    internal TSystems Start<TSystems>(
        Func<TSystems> createSystems,
        Action<TSystems>? afterSystemsRegistered = null,
        Action<TSystems>? afterPipelineAttached = null,
        Action<TSystems, ulong>? afterPostTickCommit = null)
        where TSystems : class, IRuntimeTickSystems
    {
        var systems = Configure(
            createSystems,
            afterSystemsRegistered,
            afterPipelineAttached,
            afterPostTickCommit);
        _tickScheduler.Start();
        return systems;
    }

    internal TickSchedulerStopResult Stop()
    {
        return Stop(TickScheduler.DefaultStopTimeout);
    }

    internal TickSchedulerStopResult Stop(TimeSpan timeout)
    {
        var result = _tickScheduler.TryStop(timeout);
        if (result.HasStopped && !_tickScheduler.HasActiveThread)
            DetachPipeline();

        return result;
    }

    private void DetachPipeline()
    {
        if (_pipeline == null)
            return;

        _pipeline.DetachFrom(_tickScheduler);
        _pipeline = null;
    }
}
