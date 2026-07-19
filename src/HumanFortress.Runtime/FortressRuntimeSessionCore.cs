using HumanFortress.Contracts.Content.Loading;
using HumanFortress.Content.Loading;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Diagnostics;
using HumanFortress.Runtime.Host;
using HumanFortress.Runtime.Session;
using HumanFortress.Runtime.Snapshots;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore : IFortressRuntimeHeadlessScenarioSessionPorts
{
    private readonly string _baseDir;
    private readonly bool _strictContent;
    private readonly bool _contentWarningsAsErrors;
    private readonly Action<string> _log;
    private readonly Func<string, Action<string>> _createLogCallback;
    private readonly CallbackFactoryDiagnosticSink _diagnostics;
    private readonly Action<FortressContentLoadReport> _logContentIssues;
    private readonly ulong _rngSeed;
    private readonly int _transportPlanningWorkerCount;
    private readonly RuntimeFrameSnapshotPublisher _frameSnapshots = new();
    private readonly RuntimeSessionLifecycleOwner _lifecycle;

    private RuntimeSessionServices _services => _lifecycle.Services;
    private FortressRuntimeWorkshopCompletionNotifier _workshopCompletionNotifier =>
        _lifecycle.WorkshopCompletionNotifier;
    private SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>> _runtimeSessionFactory =>
        _lifecycle.SessionFactory;
    private FortressRuntimeSession? _runtimeSession => _lifecycle.ActiveSession;
    private FortressRuntimeContentSnapshot? _runtimeContentSnapshot
    {
        get => _lifecycle.ContentSnapshot;
        set => _lifecycle.SetContentSnapshot(value);
    }

    internal FortressRuntimeSessionCore(FortressRuntimeSessionCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _baseDir = options.BaseDir;
        _strictContent = options.StrictContent;
        _contentWarningsAsErrors = options.ContentWarningsAsErrors;
        _log = options.Log ?? (_ => { });
        _createLogCallback = options.CreateLogCallback ?? (_ => _log);
        _diagnostics = new CallbackFactoryDiagnosticSink(_createLogCallback);
        _logContentIssues = options.LogContentIssues ?? (_ => { });
        _rngSeed = options.RngSeed;
        _transportPlanningWorkerCount = options.TransportPlanningWorkerCount;
        _lifecycle = new RuntimeSessionLifecycleOwner(
            _diagnostics,
            CreateRuntimeSessionFactory,
            InvalidateCheckpointGeneration,
            () => _frameSnapshots.Invalidate(),
            _log,
            _rngSeed);
    }

    internal FortressRuntimeSessionCore(
        string baseDir,
        bool strictContent,
        bool contentWarningsAsErrors,
        Action<string>? log = null,
        Func<string, Action<string>>? createLogCallback = null,
        Action<FortressContentLoadReport>? logContentIssues = null)
        : this(new FortressRuntimeSessionCoreOptions(
            baseDir,
            strictContent,
            contentWarningsAsErrors,
            log,
            createLogCallback,
            logContentIssues))
    {
    }
}
