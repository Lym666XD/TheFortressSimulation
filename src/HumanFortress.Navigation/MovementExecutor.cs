namespace HumanFortress.Navigation;

/// <summary>
/// Handles movement execution along paths with stuck detection.
/// Per NAVIGATION_SPEC.md section 7.
/// </summary>
public sealed class MovementExecutor
{
    private readonly Dictionary<uint, MovementState> _movementStates;
    private readonly IPathService _pathService;
    private readonly NavigationTuning _tuning;
    private readonly int _stepDelay;

    public MovementExecutor(IPathService pathService, NavigationTuning? tuning = null)
    {
        _movementStates = new Dictionary<uint, MovementState>();
        _pathService = pathService;
        _tuning = tuning ?? NavigationTuning.Default;
        _stepDelay = 2; // simple slowdown so movement is visible (ticks per step)
    }

    /// <summary>
    /// Start or update movement for an entity.
    /// </summary>
    public void BeginMovement(uint entityId, PathRequest request, Path path)
    {
        _movementStates[entityId] = new MovementState
        {
            Request = request,
            Path = path,
            CurrentStep = 0,
            Position = request.Source,
            StuckTicks = 0,
            LastProgress = 0,
            StepWait = 0,
        };
    }

    /// <summary>
    /// Update movement for all entities.
    /// Called during movement system update.
    /// </summary>
    public MovementUpdate UpdateMovement(uint entityId, IWorldNavigationView world)
    {
        if (!_movementStates.TryGetValue(entityId, out var state))
        {
            return new MovementUpdate(MovementStatus.NoPath, Point3.Zero, false, null);
        }

        // Check if we've reached destination
        if (state.Position == state.Request.Destination)
        {
            _movementStates.Remove(entityId);
            return new MovementUpdate(MovementStatus.Arrived, state.Position, false, null);
        }

        // Check if path is still valid
        if (state.Path.Kind != PathResultKind.Found || state.Path.Steps.Length == 0)
        {
            return new MovementUpdate(MovementStatus.NoPath, state.Position, true, null);
        }

        // Check for stuck detection
        if (state.StuckTicks > 0)
        {
            state.StuckTicks++;

            // If stuck for too long, request replan
            if (state.StuckTicks >= 10)
            {
                return new MovementUpdate(MovementStatus.Stuck, state.Position, true, null);
            }
        }

        // Delay to slow down visual movement
        if (state.StepWait < _stepDelay)
        {
            state.StepWait++;
            _movementStates[entityId] = state;
            return new MovementUpdate(MovementStatus.Moving, state.Position, false, null);
        }
        state.StepWait = 0;

        // Get next step
        if (state.CurrentStep >= state.Path.Steps.Length)
        {
            return new MovementUpdate(MovementStatus.PathComplete, state.Position, false, null);
        }

        var nextNode = state.Path.Steps.Span[state.CurrentStep];
        var nextPos = nextNode.Position;

        // Check if next position is still walkable
        if (!world.IsWalkable(nextPos, state.Request.Mode))
        {
            // Path blocked, increment stuck counter
            state.StuckTicks++;

            // Check if topology changed (connectivity version)
            var chunk = ToChunkKey(nextPos);
            var currentVersion = world.GetConnectivityVersion(chunk);

            if (currentVersion != state.LastConnectivityVersion)
            {
                // Topology changed, need replan
                return new MovementUpdate(MovementStatus.TopologyChanged, state.Position, true, null);
            }

            // Try local yielding (wait for dynamic obstacle to move)
            if (state.StuckTicks < 3)
            {
                return new MovementUpdate(MovementStatus.Waiting, state.Position, false, null);
            }
            else
            {
                return new MovementUpdate(MovementStatus.Blocked, state.Position, true, null);
            }
        }

        // Move successful
        state.Position = nextPos;
        state.CurrentStep++;
        state.StuckTicks = 0;
        state.LastProgress++;
        state.LastConnectivityVersion = world.GetConnectivityVersion(ToChunkKey(nextPos));

        // Check if we should sample ahead for smoother movement
        Point3? lookAhead = null;
        if (state.CurrentStep + 1 < state.Path.Steps.Length)
        {
            lookAhead = state.Path.Steps.Span[state.CurrentStep + 1].Position;
        }

        _movementStates[entityId] = state;

        return new MovementUpdate(MovementStatus.Moving, nextPos, false, lookAhead);
    }

    /// <summary>
    /// Cancel movement for an entity.
    /// </summary>
    public void CancelMovement(uint entityId)
    {
        _movementStates.Remove(entityId);
    }

    /// <summary>
    /// Check if entity has active movement.
    /// </summary>
    public bool HasMovement(uint entityId)
    {
        return _movementStates.ContainsKey(entityId);
    }

    /// <summary>
    /// Get movement progress for an entity.
    /// </summary>
    public float GetProgress(uint entityId)
    {
        if (!_movementStates.TryGetValue(entityId, out var state))
            return 0f;

        if (state.Path.Steps.Length == 0)
            return 0f;

        return (float)state.CurrentStep / state.Path.Steps.Length;
    }

    private static ChunkKey ToChunkKey(Point3 position)
    {
        const int ChunkSize = 32;
        return new ChunkKey(
            position.X / ChunkSize,
            position.Y / ChunkSize,
            position.Z);
    }

    /// <summary>
    /// Internal movement state for an entity.
    /// </summary>
    private struct MovementState
    {
        public PathRequest Request;
        public Path Path;
        public int CurrentStep;
        public Point3 Position;
        public int StuckTicks;
        public int LastProgress;
        public int LastConnectivityVersion;
        public int StepWait;
    }
}

/// <summary>
/// Movement status for execution.
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
/// Result of movement update.
/// </summary>
public readonly record struct MovementUpdate(
    MovementStatus Status,
    Point3 Position,
    bool NeedsReplan,
    Point3? LookAhead);
