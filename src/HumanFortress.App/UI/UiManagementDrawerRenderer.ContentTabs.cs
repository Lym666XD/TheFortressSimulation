using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiManagementDrawerRenderer
{
    private static void DrawStockDrawerContent(
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
            if (management.HasWorld)
                DrawItemsTab(surf, management.Items, ui, y0 + 2, height - 3);
            else
                surf.Print(2, y0 + 2, "(World not ready)", Color.Gray);
        }
        else if (ui.DrawerTab == 1)
        {
            management = GetManagementDrawerData(ref management, ref managementLoaded, ref hasWorld);
            if (management.HasWorld)
                DrawStockpilesTab(surf, management.Stockpiles, y0 + 2);
            else
                surf.Print(2, y0 + 2, "(World not ready)", Color.Gray);
        }
        else
        {
            surf.Print(2, y0 + 2, "(Trade coming soon)", Color.Gray);
        }
    }

}
