using HumanFortress.App.Input;

namespace HumanFortress.App.UI;

internal static class FortressUiServicesFactory
{
    public static FortressUiServices Create(
        InputBindingsService bindings,
        OrdersRegistryService ordersRegistry,
        string baseDir)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(ordersRegistry);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        Logger.Log("[GenerateFortressMap] Wiring UI classes");

        var stockpileUI = new StockpileUI();
        var ordersUI = new OrdersUI();
        var zonesUI = new ZonesUI();
        var buildUI = new BuildUI();
        var stockpileQuickUI = new StockpileQuickUI();

        bindings.Load(baseDir);
        ordersRegistry.Load(baseDir);

        return new FortressUiServices(
            stockpileUI,
            ordersUI,
            zonesUI,
            buildUI,
            stockpileQuickUI);
    }
}
