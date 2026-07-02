namespace HumanFortress.App.States;

internal sealed class FortressUiTickCounter
{
    private ulong _current;

    internal ulong Current => _current;

    internal ulong Advance()
    {
        return ++_current;
    }
}
