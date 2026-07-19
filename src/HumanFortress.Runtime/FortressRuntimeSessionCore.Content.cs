using HumanFortress.Content.Loading;
using HumanFortress.Core.Time;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Content;
using HumanFortress.Runtime.Host;
using HumanFortress.Runtime.Session;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    private void LoadSessionContent(World world)
    {
        _runtimeContentSnapshot = LoadRuntimeContentSnapshot(world);
    }

    private FortressRuntimeContentSnapshot LoadRuntimeContentSnapshot(World world)
    {
        return SimulationWorldContentLoader.LoadCoreContent(
            world,
            _baseDir,
            _strictContent,
            _contentWarningsAsErrors,
            _log,
            _logContentIssues,
            _diagnostics);
    }

    private SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>> CreateRuntimeSessionFactory(
        RuntimeSessionServices services)
    {
        return CreateRuntimeSessionFactory(
            services,
            LoadSessionContent,
            () => _runtimeContentSnapshot);
    }

    private SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>> CreateRuntimeSessionFactory(
        RuntimeSessionServices services,
        Action<World> loadContent,
        Func<FortressRuntimeContentSnapshot?> getContentSnapshot)
    {
        return new SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>>(
            services,
            loadContent,
            (world, navigation) => CreateRuntimeHost(
                services,
                world,
                navigation,
                getContentSnapshot()),
            () => NavigationTuning.LoadFromJson(getContentSnapshot()?.NavigationTuningJson));
    }

    private SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>> CreateStagedRuntimeSession(
        World world,
        bool rebuildNavigation,
        out RuntimeSessionServices stagedServices,
        out FortressRuntimeContentSnapshot? stagedContentSnapshot)
    {
        stagedServices = new RuntimeSessionServices(_diagnostics, _rngSeed);
        FortressRuntimeContentSnapshot? contentSnapshot = null;
        var stagedFactory = CreateRuntimeSessionFactory(
            stagedServices,
            stagedWorld => contentSnapshot = LoadRuntimeContentSnapshot(stagedWorld),
            () => contentSnapshot);

        var stagedSession = stagedFactory.CreateFromWorld(world, rebuildNavigation);
        stagedContentSnapshot = contentSnapshot;
        return stagedSession;
    }

    private void CommitStagedRuntimeSession(
        RuntimeSessionServices stagedServices,
        SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>> stagedSession,
        FortressRuntimeContentSnapshot? stagedContentSnapshot)
    {
        _lifecycle.CommitStagedSession(
            stagedServices,
            stagedSession,
            stagedContentSnapshot,
            ActivateCheckpointGeneration);
    }

    private SimulationRuntimeHost<SimulationRuntimeSystems> CreateRuntimeHost(
        RuntimeSessionServices services,
        World world,
        NavigationManager navigation,
        FortressRuntimeContentSnapshot? contentSnapshot)
    {
        return FortressRuntimeHostFactory.Create(
            world,
            services,
            navigation,
            _baseDir,
            contentSnapshot,
            CreateRuntimeLogging(),
            _transportPlanningWorkerCount);
    }

    private FortressRuntimeLogging CreateRuntimeLogging()
    {
        return new FortressRuntimeLogging(
            _log,
            _createLogCallback(FortressRuntimeLogBindings.ConstructionMaterialsCategory),
            _workshopCompletionNotifier);
    }
}
