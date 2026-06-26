using SadConsole;

namespace HumanFortress.App.UI
{
    /// <summary>
    /// Dormant compatibility hook for the Work drawer.
    /// </summary>
    internal static class WorkDrawerOverlay
    {
        public static void DrawWorkSchedulerOverlay(
            ScreenSurface overlay,
            UiStore ui,
            ulong tick)
        {
            if (ui.OpenDrawer != DrawerId.Work) return;

            // Inline Work drawer renders scheduler stats directly.
        }
    }
}
