using SadRogue.Primitives;

namespace HumanFortress.Runtime.Composition;

internal sealed class FortressRuntimeWorkshopCompletionNotifier
{
    private Action<int, int, int, Rectangle, string, ulong>? _handler;

    internal void SetHandler(Action<int, int, int, Rectangle, string, ulong>? handler)
    {
        _handler = handler;
    }

    internal void Notify(int x, int y, int z, Rectangle footprint, string constructionId, ulong tick)
    {
        _handler?.Invoke(x, y, z, footprint, constructionId, tick);
    }
}
