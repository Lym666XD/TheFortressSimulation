using HumanFortress.Contracts.Navigation;
using NavPath = HumanFortress.Contracts.Navigation.Path;

namespace HumanFortress.Navigation.Implementation;

/// <summary>
/// Handles movement execution along paths with stuck detection.
/// Per NAVIGATION_SPEC.md section 7.
/// </summary>
internal sealed class MovementExecutor : IMovementExecutor
{
    private readonly Dictionary<uint, MovementState> _movementStates;
    private readonly IPathService _pathService;
    private readonly NavigationTuning _tuning;
    private readonly int _stepDelay;

    internal MovementExecutor(IPathService pathService, NavigationTuning? tuning = null)
    {
        _movementStates = new Dictionary<uint, MovementState>();
        _pathService = pathService;
        _tuning = tuning ?? NavigationTuning.Default;
        _stepDelay = 2; // simple slowdown so movement is visible (ticks per step)
    }

    /// <summary>
    /// Start or update movement for an entity.
    /// </summary>
    internal void BeginMovement(uint entityId, PathRequest request, NavPath path)
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
    internal MovementUpdate UpdateMovement(uint entityId, IWorldNavigationView world)
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
    internal void CancelMovement(uint entityId)
    {
        _movementStates.Remove(entityId);
    }

    /// <summary>
    /// Check if entity has active movement.
    /// </summary>
    internal bool HasMovement(uint entityId)
    {
        return _movementStates.ContainsKey(entityId);
    }

    /// <summary>
    /// Get movement progress for an entity.
    /// </summary>
    internal float GetProgress(uint entityId)
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
        internal PathRequest Request;
        internal NavPath Path;
        internal int CurrentStep;
        internal Point3 Position;
        internal int StuckTicks;
        internal int LastProgress;
        internal int LastConnectivityVersion;
        internal int StepWait;
    }

    void IMovementExecutor.BeginMovement(uint entityId, PathRequest request, NavPath path) =>
        BeginMovement(entityId, request, path);

    MovementUpdate IMovementExecutor.UpdateMovement(uint entityId, IWorldNavigationView world) =>
        UpdateMovement(entityId, world);

    void IMovementExecutor.CancelMovement(uint entityId) => CancelMovement(entityId);

    bool IMovementExecutor.HasMovement(uint entityId) => HasMovement(entityId);

    float IMovementExecutor.GetProgress(uint entityId) => GetProgress(entityId);
}
