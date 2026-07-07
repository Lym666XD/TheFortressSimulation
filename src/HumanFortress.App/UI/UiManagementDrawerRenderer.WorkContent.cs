using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiManagementDrawerRenderer
{
    private static void DrawWorkDrawerContent(
        ICellSurface surf,
        UiStore ui,
        int y0,
        int height,
        ref SimulationWorkDrawerData work,
        ref bool workLoaded,
        ref bool hasWorld)
    {
        work = GetWorkDrawerData(ref work, ref workLoaded, ref hasWorld);
        if (!work.HasWorld)
        {
            surf.Print(2, y0 + 2, "(World not ready)", Color.Gray);
            return;
        }

        int contentHeight = height - 3;
        if (ui.DrawerTab == 0)
            UiWorkDrawerRenderer.DrawOverviewTab(surf, ui, y0 + 1, contentHeight, work);
        else if (ui.DrawerTab == 1)
            UiWorkDrawerRenderer.DrawOrdersTab(surf, y0 + 1, contentHeight, work);
        else if (ui.DrawerTab == 2)
            UiWorkDrawerRenderer.DrawJobAllocationTab(surf, ui, y0 + 1, contentHeight, work);
        else if (ui.DrawerTab == 3)
            UiWorkDrawerRenderer.DrawWorkshopOrdersTab(surf, y0 + 1, contentHeight, work);
        else
            UiWorkDrawerRenderer.DrawWorkshopsTab(surf, y0 + 1, contentHeight, work);
    }
}
