using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Diagnostics;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Stockpile;

namespace HumanFortress.Runtime.Composition;

internal static class FortressRuntimeLogBindings
{
    internal const string ConstructionMaterialsCategory = "Jobs.ConstructionMaterials";

    internal static void BindStaticCallbacks(Func<string, Action<string>> callbackFactory)
    {
        ArgumentNullException.ThrowIfNull(callbackFactory);

        NavigationManager.LogCallback = callbackFactory("Navigation.Manager");
        SimulationDiagnostics.DiagnosticSink = DiagnosticHub.Sink;
        CreatureManager.LogCallback = callbackFactory("Simulation.Creatures");
        CreaturesDiffApplicator.LogCallback = callbackFactory("Simulation.CreaturesDiff");
        ItemManager.LogCallback = callbackFactory("Simulation.Items");
        ItemsDiffApplicator.LogCallback = callbackFactory("Simulation.ItemsDiff");
        SimulationDiffApplicator.LogCallback = callbackFactory("Simulation.Diff");
        StockpileDiffApplicator.LogCallback = callbackFactory("Simulation.StockpileDiff");
        OrdersManager.LogCallback = callbackFactory("Simulation.Orders");
        MiningSystem.LogCallback = callbackFactory("Jobs.Mining");
        ConstructionMaterialsPlanner.LogCallback = callbackFactory(ConstructionMaterialsCategory);
    }
}
