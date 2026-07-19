using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Contracts.Time;
using HumanFortress.Core.Time;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;

namespace HumanFortress.Runtime.Session;

/// <summary>
/// Owns the mutable lifetime of one active Runtime generation. Session,
/// scheduler services, content, factory, and notification bridge are replaced
/// together so callers cannot observe a half-isolated generation.
/// </summary>
internal sealed class RuntimeSessionLifecycleOwner
{
    private readonly IDiagnosticSink _diagnostics;
    private readonly Func<RuntimeSessionServices, SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>>>
        _createFactory;
    private readonly Action _invalidateCheckpointGeneration;
    private readonly Action _invalidateFrameSnapshots;
    private readonly Action<string> _log;
    private readonly ulong _rngSeed;
    private readonly object _lifecycleGate = new();
    private LifecycleState _state;
    private int _disposed;

    internal RuntimeSessionLifecycleOwner(
        IDiagnosticSink diagnostics,
        Func<RuntimeSessionServices, SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>>>
            createFactory,
        Action invalidateCheckpointGeneration,
        Action invalidateFrameSnapshots,
        Action<string> log,
        ulong rngSeed = RuntimeSessionServices.DefaultRngSeed)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _createFactory = createFactory ?? throw new ArgumentNullException(nameof(createFactory));
        _invalidateCheckpointGeneration = invalidateCheckpointGeneration
            ?? throw new ArgumentNullException(nameof(invalidateCheckpointGeneration));
        _invalidateFrameSnapshots = invalidateFrameSnapshots
            ?? throw new ArgumentNullException(nameof(invalidateFrameSnapshots));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _rngSeed = rngSeed;

        var services = new RuntimeSessionServices(_diagnostics, _rngSeed);
        var notifier = new FortressRuntimeWorkshopCompletionNotifier();
        _state = new LifecycleState(
            services,
            _createFactory(services),
            ActiveSession: null,
            ContentSnapshot: null,
            notifier);
    }

    internal RuntimeSessionServices Services => Volatile.Read(ref _state).Services;

    internal FortressRuntimeSession? ActiveSession => Volatile.Read(ref _state).ActiveSession;

    internal FortressRuntimeContentSnapshot? ContentSnapshot => Volatile.Read(ref _state).ContentSnapshot;

    internal FortressRuntimeWorkshopCompletionNotifier WorkshopCompletionNotifier =>
        Volatile.Read(ref _state).WorkshopCompletionNotifier;

    internal SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>> SessionFactory
        => Volatile.Read(ref _state).SessionFactory;

    internal FortressRuntimeSession InitializeWorld(
        int sizeInChunks,
        int maxZ,
        Action<FortressRuntimeSession, RuntimeSessionServices, FortressRuntimeContentSnapshot?> activateGeneration)
    {
        ArgumentNullException.ThrowIfNull(activateGeneration);

        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            var previousServices = _state.Services;
            _ = StopCore(TickScheduler.DefaultStopTimeout);
            if (ReferenceEquals(previousServices, _state.Services))
                IsolateGenerationCore();

            var created = _state.SessionFactory.CreateNew(sizeInChunks, maxZ);
            var session = new FortressRuntimeSession(created);
            var current = _state;
            Volatile.Write(ref _state, current with { ActiveSession = session });
            activateGeneration(session, current.Services, current.ContentSnapshot);
            _invalidateFrameSnapshots();
            return session;
        }
    }

    internal bool StopIfRunning()
    {
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            var runtime = _state.ActiveSession?.Host;
            bool hadActiveScheduler = runtime?.IsRunning == true
                || runtime?.HasActiveTickThread == true;
            _ = StopCore(TickScheduler.DefaultStopTimeout);
            return hadActiveScheduler;
        }
    }

    internal TickSchedulerStopResult Stop(TimeSpan timeout)
    {
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            return StopCore(timeout);
        }
    }

    private TickSchedulerStopResult StopCore(TimeSpan timeout)
    {
        var current = _state;
        current.WorkshopCompletionNotifier.SetHandler(null);
        var runtime = current.ActiveSession?.Host;
        var result = runtime == null
            ? current.Services.TickScheduler.TryStop(timeout)
            : runtime.Stop(timeout);

        if (result.HasStopped)
            return result;

        IsolateGenerationCore();
        _log(
            $"[RuntimeLifecycle] Isolated a runtime generation after scheduler stop " +
            $"returned {result.Status} at tick {result.Tick}, phase {result.Phase}, " +
            $"system {result.SystemId ?? "<none>"}.");
        return result;
    }

    internal void Start(Action<SimulationRuntimeHost<SimulationRuntimeSystems>, RuntimeSessionServices> start)
    {
        ArgumentNullException.ThrowIfNull(start);
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            var current = _state;
            var session = current.ActiveSession
                ?? throw new InvalidOperationException("World not initialized");
            start(session.Host, current.Services);
            _invalidateFrameSnapshots();
        }
    }

    internal void ConfigureManual(
        Action<SimulationRuntimeHost<SimulationRuntimeSystems>, RuntimeSessionServices> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            var current = _state;
            var session = current.ActiveSession
                ?? throw new InvalidOperationException("World not initialized");
            if (session.Host.IsRunning || session.Host.HasActiveTickThread)
                throw new InvalidOperationException("Cannot configure manual ticks while the runtime is running.");

            configure(session.Host, current.Services);
            _invalidateFrameSnapshots();
        }
    }

    internal void SetContentSnapshot(FortressRuntimeContentSnapshot? content)
    {
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            Volatile.Write(ref _state, _state with { ContentSnapshot = content });
        }
    }

    internal void CommitStagedSession(
        RuntimeSessionServices stagedServices,
        SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>> stagedSession,
        FortressRuntimeContentSnapshot? stagedContent,
        Action<FortressRuntimeSession, RuntimeSessionServices, FortressRuntimeContentSnapshot?> activateGeneration)
    {
        ArgumentNullException.ThrowIfNull(stagedServices);
        ArgumentNullException.ThrowIfNull(stagedSession);
        ArgumentNullException.ThrowIfNull(activateGeneration);

        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            var stopResult = StopCore(TickScheduler.DefaultStopTimeout);
            if (!stopResult.HasStopped)
            {
                throw new InvalidOperationException(
                    $"Cannot commit a staged runtime session while the previous tick thread is still active " +
                    $"(status={stopResult.Status}, tick={stopResult.Tick}, phase={stopResult.Phase}, " +
                    $"system={stopResult.SystemId ?? "<none>"}).");
            }

            var current = _state;
            current.WorkshopCompletionNotifier.SetHandler(null);
            var activeSession = new FortressRuntimeSession(stagedSession);
            var replacement = new LifecycleState(
                stagedServices,
                _createFactory(stagedServices),
                activeSession,
                stagedContent,
                current.WorkshopCompletionNotifier);
            Volatile.Write(ref _state, replacement);
            activateGeneration(activeSession, stagedServices, stagedContent);
            _invalidateFrameSnapshots();
        }
    }

    private void IsolateGenerationCore()
    {
        var previous = _state;
        _invalidateCheckpointGeneration();
        previous.WorkshopCompletionNotifier.Retire();
        var services = new RuntimeSessionServices(_diagnostics, _rngSeed);
        var replacement = new LifecycleState(
            services,
            _createFactory(services),
            ActiveSession: null,
            ContentSnapshot: null,
            new FortressRuntimeWorkshopCompletionNotifier());
        Volatile.Write(ref _state, replacement);
        _invalidateFrameSnapshots();
    }

    internal void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            _ = StopCore(TickScheduler.DefaultStopTimeout);
            _invalidateCheckpointGeneration();
            var current = _state;
            current.WorkshopCompletionNotifier.SetHandler(null);
            current.WorkshopCompletionNotifier.Retire();
            Volatile.Write(
                ref _state,
                current with
                {
                    ActiveSession = null,
                    ContentSnapshot = null,
                });
            _invalidateFrameSnapshots();
            Volatile.Write(ref _disposed, 1);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }

    private sealed record LifecycleState(
        RuntimeSessionServices Services,
        SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>> SessionFactory,
        FortressRuntimeSession? ActiveSession,
        FortressRuntimeContentSnapshot? ContentSnapshot,
        FortressRuntimeWorkshopCompletionNotifier WorkshopCompletionNotifier);
}
