using HumanFortress.App.Input;
using HumanFortress.App.UI;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

internal static class FortressUiServicesFactory
{
    public static FortressUiServices Create(
        World world,
        InputBindingsService bindings,
        OrdersRegistryService ordersRegistry,
        string baseDir)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(ordersRegistry);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        System.Console.WriteLine("[GenerateFortressMap] Wiring StockpileManager & UI classes");

        var stockpileManager = world.Stockpiles;
        var stockpileUI = new StockpileUI(stockpileManager);
        var ordersUI = new OrdersUI();
        var zonesUI = new ZonesUI();
        var buildUI = new BuildUI();
        var stockpileQuickUI = new StockpileQuickUI();

        bindings.Load(baseDir);
        ordersRegistry.Load(baseDir);

        return new FortressUiServices(
            stockpileManager,
            stockpileUI,
            ordersUI,
            zonesUI,
            buildUI,
            stockpileQuickUI);
    }
}
