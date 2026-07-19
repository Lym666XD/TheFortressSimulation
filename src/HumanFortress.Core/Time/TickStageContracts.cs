namespace HumanFortress.Core.Time;

/// <summary>
/// A read-phase planning stage. Implementations may emit tick-local immutable
/// intents, but must not consume queues or mutate authoritative session state.
/// </summary>
public interface IReadPlanStage
{
    void ReadPlan(ulong tick);
}

/// <summary>
/// Temporary contract for legacy stages whose preparation and application can
/// both mutate authority. Both methods must run serially from an
/// <see cref="ITick.WriteTick"/> call; neither method is a read-phase planner.
/// </summary>
public interface ISequentialCompatibilityStage
{
    void PrepareSequentialCompatibility(ulong tick);

    void ApplySequentialCompatibility(ulong tick);
}
