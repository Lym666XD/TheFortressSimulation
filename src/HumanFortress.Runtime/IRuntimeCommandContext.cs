using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime;

public interface IRuntimeCommandContext : ISimulationContext
{
    void SetCurrentTick(ulong tick);
}
