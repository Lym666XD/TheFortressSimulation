using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal struct OrderHighlight
{
    public string Kind;
    public Rectangle Rect;
    public int ZMin;
    public int ZMax;
    public ulong ExpireTick;
}
