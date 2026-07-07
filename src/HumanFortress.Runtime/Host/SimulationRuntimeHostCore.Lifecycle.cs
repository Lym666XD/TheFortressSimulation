namespace HumanFortress.Runtime.Host;

internal sealed partial class SimulationRuntimeHostCore
{
    internal TSystems Start<TSystems>(
        Func<TSystems> createSystems,
        Action<TSystems>? afterSystemsRegistered = null,
        Action<TSystems>? afterPipelineAttached = null)
        where TSystems : class, IRuntimeTickSystems
    {
        var systems = Configure(createSystems, afterSystemsRegistered, afterPipelineAttached);
        _tickScheduler.Start();
        return systems;
    }

    internal void Stop()
    {
        StopScheduler();
        DetachPipeline();
    }

    private void StopScheduler()
    {
        _tickScheduler.Stop();
    }

    private void DetachPipeline()
    {
        if (_pipeline == null)
            return;

        _pipeline.DetachFrom(_tickScheduler);
        _pipeline = null;
    }
}
