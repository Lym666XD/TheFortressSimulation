namespace HumanFortress.Runtime.Commands;

internal interface IRuntimeCommandClockContext
{
    void SetCurrentTick(ulong tick);
}
