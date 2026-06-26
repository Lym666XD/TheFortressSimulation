using SadConsole;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.UI;

internal static partial class UiManagementDrawerRenderer
{
    public static void DrawDrawer(
        ScreenSurface mapSurface,
        UiStore ui,
        ulong tick,
        SimulationManagementDrawerData? managementData = null,
        SimulationWorkDrawerData? workData = null)
    {
        if (ui.OpenDrawer == DrawerId.None) return;
        var management = managementData ?? SimulationManagementDrawerData.Empty;
        bool managementLoaded = managementData.HasValue;
        var work = workData ?? SimulationWorkDrawerData.Empty;
        bool workLoaded = workData.HasValue;
        var surf = mapSurface.Surface;
        int height = Math.Max(10, surf.Height - 7);
        int y0 = surf.Height - 1 - height;

        DrawDrawerBackground(surf, y0);
        DrawDrawerChrome(surf, ui, y0);
        DrawDrawerContent(surf, ui, y0, height, management, managementLoaded, work, workLoaded);
    }
}
