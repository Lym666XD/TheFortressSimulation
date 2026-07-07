using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiManagementDrawerRenderer
{
    private static void DrawDrawerContent(
        ICellSurface surf,
        UiStore ui,
        int y0,
        int height,
        SimulationManagementDrawerData management,
        bool managementLoaded,
        SimulationWorkDrawerData work,
        bool workLoaded)
    {
        bool hasWorld = managementLoaded
            ? management.HasWorld
            : workLoaded
                ? work.HasWorld
                : false;

        if (ui.OpenDrawer == DrawerId.Creature && hasWorld)
        {
            management = GetManagementDrawerData(ref management, ref managementLoaded, ref hasWorld);
            if (ui.DrawerTab == 0)
                DrawCreaturesTab(surf, management.Creatures, ui, y0 + 2, height - 3);
            else if (ui.DrawerTab == 1)
                DrawAnimalsTab(surf, y0 + 2);
            else
                surf.Print(2, y0 + 2, "(Settings coming soon)", Color.Gray);
        }
        else if (ui.OpenDrawer == DrawerId.Stock)
        {
            DrawStockDrawerContent(surf, ui, y0, height, ref management, ref managementLoaded, ref hasWorld);
        }
        else if (ui.OpenDrawer == DrawerId.Work)
        {
            DrawWorkDrawerContent(surf, ui, y0, height, ref work, ref workLoaded, ref hasWorld);
        }
        else if (ui.OpenDrawer == DrawerId.PlacementManagement && hasWorld)
        {
            DrawPlacementDrawerContent(surf, ui, y0, height, ref management, ref managementLoaded, ref hasWorld);
        }
        else
        {
            surf.Print(2, y0 + 2, "(Content coming soon)", Color.Gray);
        }
    }

    private static SimulationManagementDrawerData GetManagementDrawerData(
        ref SimulationManagementDrawerData management,
        ref bool managementLoaded,
        ref bool hasWorld)
    {
        if (!managementLoaded)
        {
            management = SimulationManagementDrawerData.Empty;
            managementLoaded = true;
            hasWorld = management.HasWorld;
        }

        return management;
    }

    private static SimulationWorkDrawerData GetWorkDrawerData(
        ref SimulationWorkDrawerData work,
        ref bool workLoaded,
        ref bool hasWorld)
    {
        if (!workLoaded)
        {
            work = SimulationWorkDrawerData.Empty;
            workLoaded = true;
            hasWorld = work.HasWorld;
        }

        return work;
    }
}
