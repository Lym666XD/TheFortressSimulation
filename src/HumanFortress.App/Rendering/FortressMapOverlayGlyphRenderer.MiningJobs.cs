using System.Collections.Generic;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressMapOverlayGlyphRenderer
{
    public static void DrawMiningJobHighlights(ScreenSurface mapSurface, IReadOnlyList<JobPoint3>? targets, RuntimeViewportGeometry viewport, ulong tick)
    {
        if (targets == null || targets.Count == 0) return;
        var surf = mapSurface.Surface;
        bool flash = ((tick / 8) % 2) == 0;
        var fg = flash ? Color.Cyan : Color.DarkCyan;
        foreach (var target in targets)
        {
            if (target.Z != viewport.CurrentZ) continue;
            FortressViewportDrawing.SetWorldCellGlyph(surf, viewport, target.X, target.Y, '.', fg);
        }
    }

    public static void DrawMiningCompletedHighlights(ScreenSurface mapSurface, IReadOnlyList<JobPoint3>? completions, RuntimeViewportGeometry viewport)
    {
        if (completions == null || completions.Count == 0) return;
        var surf = mapSurface.Surface;
        var fg = new Color(255, 230, 0);
        foreach (var completion in completions)
        {
            if (completion.Z != viewport.CurrentZ) continue;
            FortressViewportDrawing.SetWorldCellGlyph(surf, viewport, completion.X, completion.Y, '.', fg);
        }
    }
}
