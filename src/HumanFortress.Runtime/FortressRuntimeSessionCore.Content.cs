using HumanFortress.Content.Loading;
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
            _logContentIssues);
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
        stagedServices = new RuntimeSessionServices();
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
        StopIfRunningCore();
        _workshopCompletionNotifier.SetHandler(null);
        _services = stagedServices ?? throw new ArgumentNullException(nameof(stagedServices));
        _runtimeSessionFactory = CreateRuntimeSessionFactory(_services);
        _runtimeSession = new FortressRuntimeSession(stagedSession);
        _runtimeContentSnapshot = stagedContentSnapshot;
        InvalidateFrameSnapshots();
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
            CreateRuntimeLogging());
    }

    private FortressRuntimeLogging CreateRuntimeLogging()
    {
        return new FortressRuntimeLogging(
            _log,
            _createLogCallback(FortressRuntimeLogBindings.ConstructionMaterialsCategory),
            _workshopCompletionNotifier);
    }
}
