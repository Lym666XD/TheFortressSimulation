using HumanFortress.Navigation.Implementation;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Navigation;

internal static class SimulationNavigationFactory
{
    internal static NavigationManager Create(World world, bool rebuildAll, NavigationTuning? tuning = null)
    {
        var manager = new NavigationManager(new SimulationNavigationSource(world), tuning);
        if (rebuildAll)
            manager.RebuildAll();
        return manager;
    }
}
