using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Content;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Host;

internal sealed partial class SimulationRuntimeHost<TSystems>
    where TSystems : class, IRuntimeTickSystems
{
    internal World World => _world;
    internal NavigationManager Navigation => _navigation;
    internal NavigationTuning NavigationTuning => _navigationTuning;
    internal IRecipeCatalog Recipes => _recipes;
    internal IConstructionCatalog Constructions => _constructions;
    internal IRuntimeGeologyCatalog Geology => _geology;
    internal FortressRuntimeStockpilePresetCatalog StockpilePresets => _stockpilePresets;
    internal TSystems? Systems => _systems;
    internal bool IsRunning => _core.IsRunning;
}
