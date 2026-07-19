using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Runtime.Session;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    private static SimulationRuntimeHost<SimulationRuntimeSystems>? Host(FortressRuntimeSession? session)
    {
        return session?.Host;
    }

    private static World? World(FortressRuntimeSession? session)
    {
        return session?.World;
    }

    private static NavigationManager? Navigation(FortressRuntimeSession? session)
    {
        return session?.Navigation;
    }

    private static NavigationTuning? NavigationTuning(FortressRuntimeSession? session)
    {
        return session?.Host.NavigationTuning;
    }

    private static RuntimeNavigationServices? NavigationServices(FortressRuntimeSession? session)
    {
        var host = session?.Host;
        return host == null
            ? null
            : new RuntimeNavigationServices(host.PathServices, host.NavigationTuning);
    }

    private static IRecipeCatalog? Recipes(FortressRuntimeSession? session)
    {
        return session?.Host.Recipes;
    }

    private static IConstructionCatalog? Constructions(FortressRuntimeSession? session)
    {
        return session?.Host.Constructions;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? WorkshopCategoryTags(
        FortressRuntimeSession? session)
    {
        return session?.Host.WorkshopCategoryTags;
    }

    private static IRuntimeGeologyCatalog? Geology(FortressRuntimeSession? session)
    {
        return session?.Host.Geology;
    }
}
