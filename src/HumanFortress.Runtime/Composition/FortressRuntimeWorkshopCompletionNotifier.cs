using SadRogue.Primitives;

namespace HumanFortress.Runtime.Composition;

internal sealed class FortressRuntimeWorkshopCompletionNotifier
{
    private Action<int, int, int, Rectangle, string, ulong>? _handler;
    private int _retired;

    internal void SetHandler(Action<int, int, int, Rectangle, string, ulong>? handler)
    {
        if (handler != null && Volatile.Read(ref _retired) != 0)
            throw new InvalidOperationException("Cannot attach a handler to a retired runtime notifier.");

        Volatile.Write(ref _handler, handler);
    }

    internal void Retire()
    {
        Interlocked.Exchange(ref _retired, 1);
        Volatile.Write(ref _handler, null);
    }

    internal void Notify(int x, int y, int z, Rectangle footprint, string constructionId, ulong tick)
    {
        if (Volatile.Read(ref _retired) != 0)
            return;

        Volatile.Read(ref _handler)?.Invoke(x, y, z, footprint, constructionId, tick);
    }
}
