using HumanFortress.Core.Time;

namespace HumanFortress.Runtime.Host;

/// <summary>
/// Runtime-visible collection of systems that participate in the tick loop.
/// </summary>
internal interface IRuntimeTickSystems
{
    void RegisterWith(TickScheduler scheduler);
}
