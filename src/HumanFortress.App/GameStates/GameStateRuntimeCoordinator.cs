using HumanFortress.App.Input;
using HumanFortress.App.Rendering;
using HumanFortress.App.Runtime;
using HumanFortress.App.Session;
using HumanFortress.App.States;
using HumanFortress.Runtime;

namespace HumanFortress.App.GameStates;

internal sealed class GameStateRuntimeCoordinator : IFortressPlayRuntimeHost, IDisposable
{
    private readonly IFortressRuntimeAppSessionPorts _runtimeSession;

    internal GameStateRuntimeCoordinator(GameStateRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _runtimeSession = FortressRuntimeSessionFactory.Create(
            configuration.BaseDirectory,
            configuration.StrictContent,
            configuration.ContentWarningsAsErrors,
            configuration.Log,
            configuration.CreateLogCallback,
            configuration.LogContentIssues);
    }

    internal FortressStateRuntimePorts CreateRuntimePorts()
    {
        var runtime = new FortressRuntimeAccess(_runtimeSession);
        return new FortressStateRuntimePorts(
            new FortressViewRuntimePorts(runtime, runtime),
            new FortressInputRuntimePorts(CreateInputDependencies(runtime)),
            new FortressSessionRuntimePorts(runtime));
    }

    private static FortressInputRuntimePortDependencies CreateInputDependencies(FortressRuntimeAccess runtime)
    {
        return new FortressInputRuntimePortDependencies(
            BuildCatalog: runtime,
            ZoneCatalog: runtime,
            WorkshopQueries: runtime,
            WorkshopCommands: runtime,
            SimulationControl: runtime,
            UiInput: runtime,
            PlacementQueries: runtime,
            PlacementCommands: runtime,
            DebugSpawnQueries: runtime,
            DebugSpawnCommands: runtime,
            MapInspection: runtime);
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

    internal void Dispose()
    {
        _runtimeSession.Dispose();
    }

    void IDisposable.Dispose() => Dispose();

    FortressStateRuntimePorts IFortressPlayRuntimeHost.CreateRuntimePorts() => CreateRuntimePorts();

    void IFortressPlayRuntimeHost.InitializeWorld(int sizeInChunks, int maxZ) => InitializeWorld(sizeInChunks, maxZ);
}
