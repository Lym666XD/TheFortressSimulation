using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    public static void DrawOverviewTab(ICellSurface surf, UiStore ui, int startY, int maxHeight, SimulationWorkDrawerData work)
    {
        var layout = BuildWorkPanelLayout(surf, startY, maxHeight);
        DecorateWorkPanel(surf, layout);
        RenderLaborSummaryColumn(surf, layout.Left, work);
        RenderDwarfRosterColumn(surf, layout.Center, ui, work);
        RenderSchedulerColumn(surf, layout.Right, "Scheduler Diagnostics", work);
    }

    public static void DrawOrdersTab(ICellSurface surf, int startY, int maxHeight, SimulationWorkDrawerData work)
    {
        var layout = BuildWorkPanelLayout(surf, startY, maxHeight);
        DecorateWorkPanel(surf, layout);
        RenderOrdersSummaryColumn(surf, layout.Left, work);
        RenderJobsColumn(surf, layout.Center, work);
        RenderSchedulerColumn(surf, layout.Right, "Workshop Stats", work);
    }

    public static void DrawWorkshopOrdersTab(ICellSurface surf, int startY, int maxHeight, SimulationWorkDrawerData work)
    {
        var layout = BuildWorkPanelLayout(surf, startY, maxHeight);
        var workshops = work.Workshops;
        DecorateWorkPanel(surf, layout);
        RenderWorkshopListColumn(surf, layout.Left, workshops);
        RenderWorkshopNotesColumn(surf, layout.Center, workshops);
        RenderSchedulerColumn(surf, layout.Right, "Workshop Stats", work);
    }

    public static void DrawWorkshopsTab(ICellSurface surf, int startY, int maxHeight, SimulationWorkDrawerData work)
    {
        var layout = BuildWorkPanelLayout(surf, startY, maxHeight);
        var workshops = work.Workshops;
        DecorateWorkPanel(surf, layout);
        RenderStandingOrdersColumn(surf, layout.Left);
        RenderWorkshopDirectory(surf, layout.Center, workshops);
        RenderConstructionStatusColumn(surf, layout.Right, workshops, work);
    }

}
