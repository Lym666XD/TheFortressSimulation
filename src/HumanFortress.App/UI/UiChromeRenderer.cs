using HumanFortress.Contracts.Runtime;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiChromeRenderer
{
    public static void DrawTopBar(ScreenSurface overlay, SimulationStatus? simulationStatus = null)
    {
        var surf = overlay.Surface;
        const int y = 0;
        for (int x = 0; x < surf.Width; x++)
            surf.SetGlyph(x, y, ' ', Color.White, new Color(10, 10, 10));

        string statusText = "";
        Color statusColor = Color.Gray;

        if (simulationStatus.HasValue)
        {
            var status = simulationStatus.Value;
            if (status.IsPaused)
            {
                statusText = "[PAUSED]";
                statusColor = Color.Yellow;
            }
            else
            {
                statusText = $"[{status.SpeedMultiplier:F2}x]";
                statusColor = status.SpeedMultiplier switch
                {
                    < 1.0f => Color.Cyan,
                    1.0f => Color.White,
                    _ => Color.Orange
                };
            }

            surf.Print(1, y, statusText, statusColor);
            surf.Print(1 + statusText.Length + 2, y, "[Space] Pause  [-] Slower  [+] Faster", Color.Gray);
        }
        else
        {
            surf.Print(1, y, "[Space] Pause  [-] Slower  [+] Faster", Color.Gray);
        }
    }
}
