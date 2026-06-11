using HumanFortress.Navigation;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

public static class SimulationNavigationFactory
{
    public static NavigationManager Create(World world, bool rebuildAll)
    {
        var manager = new NavigationManager(new SimulationNavigationSource(world));
        if (rebuildAll)
            manager.RebuildAll();
        return manager;
    }
}
