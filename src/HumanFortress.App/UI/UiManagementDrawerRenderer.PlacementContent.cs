using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiManagementDrawerRenderer
{
    private static void DrawPlacementDrawerContent(
        ICellSurface surf,
        UiStore ui,
        int y0,
        int height,
        ref SimulationManagementDrawerData management,
        ref bool managementLoaded,
        ref bool hasWorld)
    {
        if (ui.DrawerTab == 0)
        {
            management = GetManagementDrawerData(ref management, ref managementLoaded, ref hasWorld);
            DrawZonesTab(surf, management.Zones, y0 + 2, height - 3);
        }
        else if (ui.DrawerTab == 1)
        {
            management = GetManagementDrawerData(ref management, ref managementLoaded, ref hasWorld);
            DrawStockpilesTab(surf, management.Stockpiles, y0 + 2);
        }
        else
        {
            surf.Print(2, y0 + 2, "(Settings coming soon)", Color.Gray);
        }
    }
}
