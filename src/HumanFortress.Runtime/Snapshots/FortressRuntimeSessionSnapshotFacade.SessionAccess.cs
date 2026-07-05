using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    private static SimulationRuntimeHost<SimulationRuntimeSystems>? Host(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return session?.Host;
    }

    private static World? World(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return session?.World;
    }

    private static NavigationManager? Navigation(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return session?.Navigation;
    }

    private static NavigationTuning? NavigationTuning(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return session?.Host.NavigationTuning;
    }

    private static IRecipeCatalog? Recipes(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return session?.Host.Recipes;
    }

    private static IConstructionCatalog? Constructions(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return session?.Host.Constructions;
    }

    private static IRuntimeGeologyCatalog? Geology(HumanFortress.Runtime.Session.SimulationRuntimeSession<HumanFortress.Runtime.Host.SimulationRuntimeHost<HumanFortress.Runtime.Composition.SimulationRuntimeSystems>>? session)
    {
        return session?.Host.Geology;
    }
}
