using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Rendering;

internal static class FortressUiModalRenderer
{
    public static void Render(
        FortressUiOverlayRenderContext context,
        SimulationUiOverlayFrameData overlayData)
    {
        var uiSurface = context.UiSurface;
        var ui = context.Ui;
        var diagnostics = ui.DebugOpen ? context.Diagnostics.GetSnapshot() : null;
        var debugMenu = overlayData.DebugMenu ?? default;

        UiDebugMenuRenderer.Draw(
            uiSurface,
            ui,
            context.CursorPosition,
            context.CurrentZ,
            context.ZoomLevel,
            context.CameraPosition,
            context.FortressSize,
            debugMenu,
            diagnostics);

        FortressDebugUnitOverlayRenderer.Draw(uiSurface, ui, context.CameraPosition.X, context.CameraPosition.Y, context.CurrentZ);
        UiChromeRenderer.DrawHelp(uiSurface, ui);
        UiChromeRenderer.DrawPause(uiSurface, ui);

        if (ui.ConstructionMaterialDialogOpen && context.UiServices?.BuildUI != null)
        {
            context.UiServices.BuildUI.DrawConstructionMaterialDialog(uiSurface, ui);
        }

        UiWorkshopPanelRenderer.Draw(uiSurface, ui, overlayData.Workshops);

        UiChromeRenderer.DrawToasts(uiSurface, ui, context.UiTick);
    }
}
