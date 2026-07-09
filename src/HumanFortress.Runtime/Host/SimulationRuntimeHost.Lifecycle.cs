namespace HumanFortress.Runtime.Host;

internal sealed partial class SimulationRuntimeHost<TSystems>
    where TSystems : class, IRuntimeTickSystems
{
    internal void Start(Action<TSystems>? afterPipelineAttached = null)
    {
        _systems = null;
        _systems = _core.Start(
            _createSystems,
            systems => _afterSystemsRegistered?.Invoke(_commandContext.ProfessionCommandBindings, systems),
            afterPipelineAttached);
    }

    internal TSystems AttachForManualTicks(Action<TSystems>? afterPipelineAttached = null)
    {
        _systems = null;
        _systems = _core.Configure(
            _createSystems,
            systems => _afterSystemsRegistered?.Invoke(_commandContext.ProfessionCommandBindings, systems),
            afterPipelineAttached);
        return _systems;
    }

    internal void Stop()
    {
        _core.Stop();
    }
}
