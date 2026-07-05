namespace HumanFortress.Runtime.Commands;

internal sealed partial class SimulationCommandExecutionContext :
    IRuntimeCommandClockContext
{
    void IRuntimeCommandClockContext.SetCurrentTick(ulong tick)
    {
        _clockContext.SetCurrentTick(tick);
    }
}
