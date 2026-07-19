namespace HumanFortress.Contracts.Navigation;

/// <summary>
/// Executes movement along solved paths while tracking per-entity movement state.
/// Concrete implementations live outside Jobs so job executors only depend on
/// navigation contracts.
/// </summary>
public interface IMovementExecutor
{
    IMovementMutationScope BeginMutationScope();

    void BeginMovement(ulong entityKey, PathRequest request, Path path);

    MovementUpdate UpdateMovement(ulong entityKey, IWorldNavigationView world);

    MovementProposalData PlanMovement(ulong entityKey, IWorldNavigationView world);

    bool TryCommitMovement(MovementProposalData proposal);

    MovementCursorData? GetCursorSnapshot(ulong entityKey);

    void CancelMovement(ulong entityKey);

    bool HasMovement(ulong entityKey);

    float GetProgress(ulong entityKey);
}

/// <summary>
/// Failure-atomic scope for a serialized movement commit. Disposing an
/// uncommitted scope restores every cursor to its entry state.
/// </summary>
public interface IMovementMutationScope : IDisposable
{
    void Commit();
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
    Point3? LookAhead,
    byte SearchAttempt = 0);

/// <summary>
/// Immutable movement authority for one entity. Revision is a per-entity CAS
/// generation; path steps are owned copies at implementation boundaries.
/// </summary>
public readonly record struct MovementCursorData(
    ulong EntityKey,
    ulong Revision,
    PathRequest Request,
    Path Path,
    int CurrentStep,
    Point3 Position,
    int StuckTicks,
    int LastProgress,
    int LastConnectivityVersion,
    int StepWait);

/// <summary>
/// Pure result of planning one movement update. Authority changes only when
/// TryCommitMovement accepts ExpectedRevision.
/// </summary>
public readonly record struct MovementProposalData(
    ulong EntityKey,
    ulong ExpectedRevision,
    MovementCursorData? NextCursor,
    MovementUpdate Update);
