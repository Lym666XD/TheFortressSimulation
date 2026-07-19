using HumanFortress.Contracts.Navigation;
using NavPath = HumanFortress.Contracts.Navigation.Path;

namespace HumanFortress.Navigation.Implementation;

/// <summary>
/// Handles movement execution along paths with stuck detection.
/// Per NAVIGATION_SPEC.md section 7.
/// </summary>
internal sealed class MovementExecutor : IMovementExecutor
{
    private readonly Dictionary<ulong, MovementState> _movementStates;
    private readonly IPathService _pathService;
    private readonly NavigationTuning _tuning;
    private readonly int _stepDelay;
    private readonly object _stateLock = new();

    internal MovementExecutor(IPathService pathService, NavigationTuning? tuning = null)
    {
        _movementStates = new Dictionary<ulong, MovementState>();
        _pathService = pathService;
        _tuning = tuning ?? NavigationTuning.Default;
        _stepDelay = _tuning.MovementStepDelayTicks;
    }

    internal IMovementMutationScope BeginMutationScope()
    {
        lock (_stateLock)
        {
            return new MutationScope(this, CloneStates(_movementStates));
        }
    }

    /// <summary>
    /// Start or update movement for an entity.
    /// </summary>
    internal void BeginMovement(ulong entityKey, PathRequest request, NavPath path)
    {
        if (path.Kind == PathResultKind.Found && !path.ReachesDestination(request.Destination))
        {
            throw new ArgumentException(
                "A Found path must end at the movement request destination.",
                nameof(path));
        }

        lock (_stateLock)
        {
            ulong revision = _movementStates.TryGetValue(entityKey, out var previous)
                ? NextRevision(previous.Revision)
                : 1;
            _movementStates[entityKey] = new MovementState
            {
                Revision = revision,
                Request = request,
                Path = ClonePath(path),
                CurrentStep = 0,
                Position = request.Source,
                StuckTicks = 0,
                LastProgress = 0,
                StepWait = 0,
            };
        }
    }

    /// <summary>
    /// Update movement for all entities.
    /// Called during movement system update.
    /// </summary>
    internal MovementUpdate UpdateMovement(ulong entityKey, IWorldNavigationView world)
    {
        var proposal = PlanMovement(entityKey, world);
        if (proposal.ExpectedRevision != 0 && !TryCommitMovement(proposal))
        {
            throw new InvalidOperationException(
                $"Movement cursor {entityKey} changed between serial plan and commit.");
        }

        return proposal.Update;
    }

    internal MovementProposalData PlanMovement(ulong entityKey, IWorldNavigationView world)
    {
        ArgumentNullException.ThrowIfNull(world);
        MovementState state;
        lock (_stateLock)
        {
            if (!_movementStates.TryGetValue(entityKey, out state))
            {
                return new MovementProposalData(
                    entityKey,
                    ExpectedRevision: 0,
                    NextCursor: null,
                    new MovementUpdate(MovementStatus.NoPath, Point3.Zero, false, null));
            }
        }

        ulong expectedRevision = state.Revision;

        MovementProposalData Keep(MovementUpdate update)
        {
            state.Revision = NextRevision(expectedRevision);
            return new MovementProposalData(
                entityKey,
                expectedRevision,
                ToCursor(entityKey, state),
                update);
        }

        MovementProposalData Remove(MovementUpdate update)
        {
            return new MovementProposalData(
                entityKey,
                expectedRevision,
                NextCursor: null,
                update);
        }

        // Check if we've reached destination
        if (state.Position == state.Request.Destination)
        {
            return Remove(new MovementUpdate(
                MovementStatus.Arrived,
                state.Position,
                false,
                null));
        }

        // Check if path is still valid
        if (state.Path.Kind != PathResultKind.Found || state.Path.Steps.Length == 0)
        {
            byte retryAttempt = state.Path.Kind == PathResultKind.Partial
                ? state.Request.NextSearchAttempt().SearchAttempt
                : state.Request.SearchAttempt;
            return Keep(new MovementUpdate(
                MovementStatus.NoPath,
                state.Position,
                true,
                null,
                retryAttempt));
        }

        // Check for stuck detection
        if (state.StuckTicks > 0)
        {
            state.StuckTicks++;

            // If stuck for too long, request replan
            if (state.StuckTicks >= 10)
            {
                return Keep(new MovementUpdate(MovementStatus.Stuck, state.Position, true, null));
            }
        }

        // Deterministic pacing between movement steps.
        if (state.StepWait < _stepDelay)
        {
            state.StepWait++;
            return Keep(new MovementUpdate(MovementStatus.Moving, state.Position, false, null));
        }
        state.StepWait = 0;

        // Get next step
        if (state.CurrentStep >= state.Path.Steps.Length)
        {
            return Keep(new MovementUpdate(MovementStatus.PathComplete, state.Position, true, null));
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
                return Keep(new MovementUpdate(MovementStatus.TopologyChanged, state.Position, true, null));
            }

            // Try local yielding (wait for dynamic obstacle to move)
            if (state.StuckTicks < 3)
            {
                return Keep(new MovementUpdate(MovementStatus.Waiting, state.Position, false, null));
            }
            else
            {
                return Keep(new MovementUpdate(MovementStatus.Blocked, state.Position, true, null));
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

        return Keep(new MovementUpdate(MovementStatus.Moving, nextPos, false, lookAhead));
    }

    internal bool TryCommitMovement(MovementProposalData proposal)
    {
        if (proposal.ExpectedRevision == 0)
            return false;

        lock (_stateLock)
        {
            if (!_movementStates.TryGetValue(proposal.EntityKey, out var current)
                || current.Revision != proposal.ExpectedRevision)
            {
                return false;
            }

            if (!proposal.NextCursor.HasValue)
                return _movementStates.Remove(proposal.EntityKey);

            var next = proposal.NextCursor.Value;
            if (next.EntityKey != proposal.EntityKey
                || next.Revision != NextRevision(proposal.ExpectedRevision))
            {
                return false;
            }

            _movementStates[proposal.EntityKey] = FromCursor(next);
            return true;
        }
    }

    internal MovementCursorData? GetCursorSnapshot(ulong entityKey)
    {
        lock (_stateLock)
        {
            return _movementStates.TryGetValue(entityKey, out var state)
                ? ToCursor(entityKey, state)
                : null;
        }
    }

    /// <summary>
    /// Cancel movement for an entity.
    /// </summary>
    internal void CancelMovement(ulong entityKey)
    {
        lock (_stateLock)
        {
            _movementStates.Remove(entityKey);
        }
    }

    /// <summary>
    /// Check if entity has active movement.
    /// </summary>
    internal bool HasMovement(ulong entityKey)
    {
        lock (_stateLock)
        {
            return _movementStates.ContainsKey(entityKey);
        }
    }

    /// <summary>
    /// Get movement progress for an entity.
    /// </summary>
    internal float GetProgress(ulong entityKey)
    {
        lock (_stateLock)
        {
            if (!_movementStates.TryGetValue(entityKey, out var state))
                return 0f;

            if (state.Path.Steps.Length == 0)
                return 0f;

            return (float)state.CurrentStep / state.Path.Steps.Length;
        }
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
        internal ulong Revision;
        internal PathRequest Request;
        internal NavPath Path;
        internal int CurrentStep;
        internal Point3 Position;
        internal int StuckTicks;
        internal int LastProgress;
        internal int LastConnectivityVersion;
        internal int StepWait;
    }

    private static MovementCursorData ToCursor(ulong entityKey, MovementState state)
    {
        return new MovementCursorData(
            entityKey,
            state.Revision,
            state.Request,
            ClonePath(state.Path),
            state.CurrentStep,
            state.Position,
            state.StuckTicks,
            state.LastProgress,
            state.LastConnectivityVersion,
            state.StepWait);
    }

    private static MovementState FromCursor(MovementCursorData cursor)
    {
        return new MovementState
        {
            Revision = cursor.Revision,
            Request = cursor.Request,
            Path = ClonePath(cursor.Path),
            CurrentStep = cursor.CurrentStep,
            Position = cursor.Position,
            StuckTicks = cursor.StuckTicks,
            LastProgress = cursor.LastProgress,
            LastConnectivityVersion = cursor.LastConnectivityVersion,
            StepWait = cursor.StepWait,
        };
    }

    private static NavPath ClonePath(NavPath path)
    {
        return path with { Steps = path.Steps.ToArray() };
    }

    private static ulong NextRevision(ulong revision)
    {
        return revision == ulong.MaxValue
            ? throw new InvalidOperationException("Movement cursor revision exhausted.")
            : revision + 1;
    }

    private static Dictionary<ulong, MovementState> CloneStates(
        IReadOnlyDictionary<ulong, MovementState> source)
    {
        return source.ToDictionary(
            static entry => entry.Key,
            static entry => CloneState(entry.Value));
    }

    private static MovementState CloneState(MovementState state)
    {
        state.Path = ClonePath(state.Path);
        return state;
    }

    private sealed class MutationScope : IMovementMutationScope
    {
        private readonly MovementExecutor _owner;
        private Dictionary<ulong, MovementState>? _entryState;
        private bool _committed;

        internal MutationScope(
            MovementExecutor owner,
            Dictionary<ulong, MovementState> entryState)
        {
            _owner = owner;
            _entryState = entryState;
        }

        public void Commit()
        {
            ObjectDisposedException.ThrowIf(_entryState == null, this);
            _committed = true;
        }

        public void Dispose()
        {
            var entryState = Interlocked.Exchange(ref _entryState, null);
            if (entryState == null || _committed)
                return;

            lock (_owner._stateLock)
            {
                _owner._movementStates.Clear();
                foreach (var entry in entryState)
                    _owner._movementStates.Add(entry.Key, CloneState(entry.Value));
            }
        }
    }

    IMovementMutationScope IMovementExecutor.BeginMutationScope() => BeginMutationScope();

    void IMovementExecutor.BeginMovement(ulong entityKey, PathRequest request, NavPath path) =>
        BeginMovement(entityKey, request, path);

    MovementUpdate IMovementExecutor.UpdateMovement(ulong entityKey, IWorldNavigationView world) =>
        UpdateMovement(entityKey, world);

    MovementProposalData IMovementExecutor.PlanMovement(ulong entityKey, IWorldNavigationView world) =>
        PlanMovement(entityKey, world);

    bool IMovementExecutor.TryCommitMovement(MovementProposalData proposal) =>
        TryCommitMovement(proposal);

    MovementCursorData? IMovementExecutor.GetCursorSnapshot(ulong entityKey) =>
        GetCursorSnapshot(entityKey);

    void IMovementExecutor.CancelMovement(ulong entityKey) => CancelMovement(entityKey);

    bool IMovementExecutor.HasMovement(ulong entityKey) => HasMovement(entityKey);

    float IMovementExecutor.GetProgress(ulong entityKey) => GetProgress(entityKey);
}
