using HumanFortress.Contracts.Navigation;
using HumanFortress.Navigation.Implementation;
using NavPath = HumanFortress.Contracts.Navigation.Path;

namespace HumanFortress.Runtime.Navigation;

internal sealed class RuntimeNavigationServices
{
    private readonly RuntimePathServiceRegistry? _pathServices;
    private readonly NavigationTuning _tuning;

    internal RuntimeNavigationServices(
        RuntimePathServiceRegistry? pathServices,
        NavigationTuning? tuning = null)
    {
        _pathServices = pathServices;
        _tuning = tuning ?? NavigationTuning.Default;
    }

    internal RuntimeJobNavigationServices CreateJobServices(
        NavigationManager navigation,
        IPathService? pathService = null)
    {
        var query = CreatePathQueryServices(navigation, pathService);

        return new RuntimeJobNavigationServices(
            PathService: query.PathService,
            WorldView: query.WorldView,
            Movement: new MovementExecutor(query.PathService, _tuning));
    }

    internal RuntimeNavigationPathQueryServices CreatePathQueryServices(
        NavigationManager navigation,
        IPathService? pathService = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        var paths = pathService ?? new PathService(_tuning);
        _pathServices?.Register(paths);

        return new RuntimeNavigationPathQueryServices(
            PathService: paths,
            WorldView: new WorldNavigationView(navigation));
    }

    internal NavPath FindPath(
        NavigationManager navigation,
        in PathRequest request,
        IPathService? pathService = null)
    {
        var query = CreatePathQueryServices(navigation, pathService);
        var worldView = query.WorldView;
        return query.PathService.Solve(in request, in worldView);
    }
}

internal readonly record struct RuntimeJobNavigationServices(
    IPathService PathService,
    IWorldNavigationView WorldView,
    IMovementExecutor Movement);

internal readonly record struct RuntimeNavigationPathQueryServices(
    IPathService PathService,
    IWorldNavigationView WorldView);
