namespace HumanFortress.Contracts.Runtime.Snapshots;

public static class SimulationSnapshotSchema
{
    public const int CurrentVersion = 1;
}

public readonly record struct SimulationSnapshotMetadata(
    int SchemaVersion,
    ulong RuntimeTick)
{
    public static SimulationSnapshotMetadata Current(ulong runtimeTick)
    {
        return new SimulationSnapshotMetadata(
            SimulationSnapshotSchema.CurrentVersion,
            runtimeTick);
    }
}

public readonly record struct SnapshotColor(int R, int G, int B);

public readonly record struct SnapshotPoint(int X, int Y);
