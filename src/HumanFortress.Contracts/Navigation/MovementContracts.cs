namespace HumanFortress.Contracts.Navigation;

/// <summary>
/// Executes movement along solved paths while tracking per-entity movement state.
/// Concrete implementations live outside Jobs so job executors only depend on
/// navigation contracts.
/// </summary>
public interface IMovementExecutor
{
    void BeginMovement(uint entityId, PathRequest request, Path path);

    MovementUpdate UpdateMovement(uint entityId, IWorldNavigationView world);

    void CancelMovement(uint entityId);

    bool HasMovement(uint entityId);

    float GetProgress(uint entityId);
}

/// <summary>
/// Status emitted by a movement executor update.
/// </summary>
public enum MovementStatus
{
    /// <summary>Entity is moving along path.</summary>
    Moving,

    /// <summary>Entity arrived at destination.</summary>
    Arrived,

    /// <summary>Entity is waiting (temporary obstacle).</summary>
    Waiting,

    /// <summary>Path is blocked.</summary>
    Blocked,

    /// <summary>Entity is stuck (needs intervention).</summary>
    Stuck,

    /// <summary>No valid path exists.</summary>
    NoPath,

    /// <summary>Path completed but not at destination.</summary>
    PathComplete,

    /// <summary>Topology changed, need replan.</summary>
    TopologyChanged,
}

/// <summary>
/// Result of a movement update.
/// </summary>
public readonly record struct MovementUpdate(
    MovementStatus Status,
    Point3 Position,
    bool NeedsReplan,
    Point3? LookAhead);
