using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Content;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Host;

internal sealed partial class SimulationRuntimeHost<TSystems>
    where TSystems : class, IRuntimeTickSystems
{
    internal World World => _world;
    internal NavigationManager Navigation => _navigation;
    internal NavigationTuning NavigationTuning => _navigationTuning;
    internal RuntimePathServiceRegistry? PathServices => _pathServices;
    internal IRecipeCatalog Recipes => _recipes;
    internal IConstructionCatalog Constructions => _constructions;
    internal IRuntimeGeologyCatalog Geology => _geology;
    internal FortressRuntimeStockpilePresetCatalog StockpilePresets => _stockpilePresets;
    internal TSystems? Systems => _systems;
    internal bool IsRunning => _core.IsRunning;

    internal TSystems RequireSystems()
    {
        return _systems
            ?? throw new InvalidOperationException("Runtime systems have not been configured for this host.");
    }
}
