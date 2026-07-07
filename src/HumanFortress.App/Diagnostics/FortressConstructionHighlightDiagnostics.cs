using HumanFortress.App.UI;

namespace HumanFortress.App.Diagnostics;

internal static class FortressConstructionHighlightDiagnostics
{
    public static void Log(UiStore ui, ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(ui);

        if ((uiTick % 30UL) != 0UL)
            return;

        try
        {
            var count = ui.GetHighlights().Count;
            Logger.Log($"[BUILD.UI] highlight active={count} placeMode={ui.PlaceMode}");
        }
        catch
        {
        }
    }
}
