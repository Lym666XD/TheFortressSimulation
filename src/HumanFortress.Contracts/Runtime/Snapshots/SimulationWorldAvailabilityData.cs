using HumanFortress.Contracts.Runtime;

namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct SimulationWorldAvailabilityData(
    bool HasWorld,
    RuntimeWorldBounds WorldBounds = default)
{
    public static SimulationWorldAvailabilityData Empty { get; } = new(false, RuntimeWorldBounds.Empty);
}
