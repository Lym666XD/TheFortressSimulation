namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct SimulationWorldAvailabilityData(bool HasWorld)
{
    public static SimulationWorldAvailabilityData Empty { get; } = new(false);
}
