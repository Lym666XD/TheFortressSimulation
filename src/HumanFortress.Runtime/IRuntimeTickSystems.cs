using HumanFortress.Core.Time;

namespace HumanFortress.Runtime;

/// <summary>
/// Runtime-visible collection of systems that participate in the tick loop.
/// </summary>
public interface IRuntimeTickSystems
{
    void RegisterWith(TickScheduler scheduler);
}
