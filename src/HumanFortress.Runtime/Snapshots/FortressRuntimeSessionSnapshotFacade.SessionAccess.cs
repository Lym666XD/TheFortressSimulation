using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Navigation;
using HumanFortress.Simulation.World;
using Session = HumanFortress.Runtime.SimulationRuntimeSession<HumanFortress.Runtime.SimulationRuntimeHost<HumanFortress.Runtime.SimulationRuntimeSystems>>;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    private static SimulationRuntimeHost<SimulationRuntimeSystems>? Host(Session? session)
    {
        return session?.Host;
    }

    private static World? World(Session? session)
    {
        return session?.World;
    }

    private static NavigationManager? Navigation(Session? session)
    {
        return session?.Navigation;
    }

    private static NavigationTuning? NavigationTuning(Session? session)
    {
        return session?.Host.NavigationTuning;
    }

    private static IRecipeCatalog? Recipes(Session? session)
    {
        return session?.Host.Recipes;
    }

    private static IConstructionCatalog? Constructions(Session? session)
    {
        return session?.Host.Constructions;
    }

    private static IRuntimeGeologyCatalog? Geology(Session? session)
    {
        return session?.Host.Geology;
    }
}
