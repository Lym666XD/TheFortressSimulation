using System.Collections.Generic;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class UiStore
{
    public readonly List<(string text, ulong expireTick)> Toasts = new();

    private readonly List<OrderHighlight> _highlights = new();

    public void AddToast(string text, ulong expireTick)
    {
        Toasts.Add((text, expireTick));
    }

    public void AddHighlight(string kind, Rectangle rect, int zMin, int zMax, ulong expireTick)
    {
        _highlights.Add(new OrderHighlight { Kind = kind, Rect = rect, ZMin = zMin, ZMax = zMax, ExpireTick = expireTick });
    }

    public IReadOnlyList<OrderHighlight> GetHighlights() => _highlights;

    public void PruneHighlights(ulong nowTick)
    {
        for (int i = _highlights.Count - 1; i >= 0; i--)
        {
            if (_highlights[i].ExpireTick <= nowTick)
                _highlights.RemoveAt(i);
        }
    }

    public void PruneToasts(ulong nowTick)
    {
        for (int i = Toasts.Count - 1; i >= 0; i--)
        {
            if (Toasts[i].expireTick <= nowTick)
                Toasts.RemoveAt(i);
        }
    }
}
