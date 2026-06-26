using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;
using System.Linq;

namespace HumanFortress.App.UI;

internal static partial class UiManagementDrawerRenderer
{
    private static void DrawStockpilesTab(ICellSurface surf, StockpileDrawerData stockpiles, int startY)
    {
        var rows = stockpiles.Stockpiles;
        surf.Print(2, startY, $"Stockpiles: {rows.Count}", Color.Yellow);
        int y = startY + 2;
        foreach (var zone in rows.Take(10))
        {
            surf.Print(2, y++, $"#{zone.ZoneId} {zone.Name} Pri:{zone.Priority}", Color.White);
        }
    }
}
