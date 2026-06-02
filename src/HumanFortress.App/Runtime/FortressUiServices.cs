using HumanFortress.App.UI;
using HumanFortress.Simulation.Stockpile;

namespace HumanFortress.App.Runtime;

internal sealed record FortressUiServices(
    StockpileManager StockpileManager,
    StockpileUI StockpileUI,
    OrdersUI OrdersUI,
    ZonesUI ZonesUI,
    BuildUI BuildUI,
    StockpileQuickUI StockpileQuickUI);
