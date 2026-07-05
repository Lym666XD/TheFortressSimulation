using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Content;
using HumanFortress.Runtime.Host;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    private void LoadSessionContent(World world)
    {
        _runtimeContentSnapshot = null;
        _runtimeContentSnapshot = SimulationWorldContentLoader.LoadCoreContent(
            world,
            _baseDir,
            _strictContent,
            _contentWarningsAsErrors,
            _log,
            _logContentIssues);
    }

    private SimulationRuntimeHost<SimulationRuntimeSystems> CreateRuntimeHost(
        World world,
        NavigationManager navigation)
    {
        return FortressRuntimeHostFactory.Create(
            world,
            _services,
            navigation,
            _baseDir,
            _runtimeContentSnapshot,
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
