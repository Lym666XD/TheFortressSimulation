using System.Collections.Generic;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressMapOverlayGlyphRenderer
{
    public static void DrawMiningJobHighlights(ScreenSurface mapSurface, IReadOnlyList<JobPoint3>? targets, Point camera, int currentZ, ulong tick)
    {
        if (targets == null || targets.Count == 0) return;
        var surf = mapSurface.Surface;
        bool flash = ((tick / 8) % 2) == 0;
        var fg = flash ? Color.Cyan : Color.DarkCyan;
        foreach (var target in targets)
        {
            if (target.Z != currentZ) continue;
            int sx = target.X - camera.X;
            int sy = target.Y - camera.Y;
            if (sx >= 0 && sx < surf.Width && sy >= 0 && sy < surf.Height)
            {
                surf.SetGlyph(sx, sy, '.', fg, Color.Transparent);
            }
        }
    }

    public static void DrawMiningCompletedHighlights(ScreenSurface mapSurface, IReadOnlyList<JobPoint3>? completions, Point camera, int currentZ)
    {
        if (completions == null || completions.Count == 0) return;
        var surf = mapSurface.Surface;
        var fg = new Color(255, 230, 0);
        foreach (var completion in completions)
        {
            if (completion.Z != currentZ) continue;
            int sx = completion.X - camera.X;
            int sy = completion.Y - camera.Y;
            if (sx >= 0 && sx < surf.Width && sy >= 0 && sy < surf.Height)
            {
                surf.SetGlyph(sx, sy, '.', fg, Color.Transparent);
            }
        }
    }
}
