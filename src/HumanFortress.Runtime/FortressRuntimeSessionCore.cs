using HumanFortress.Content.Loading;
using HumanFortress.Navigation;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore : IFortressRuntimeSessionPorts
{
    private readonly string _baseDir;
    private readonly RuntimeSessionServices _services;
    private readonly bool _strictContent;
    private readonly bool _contentWarningsAsErrors;
    private readonly Action<string> _log;
    private readonly Func<string, Action<string>> _createLogCallback;
    private readonly Action<FortressContentLoadResult> _logContentIssues;
    private readonly FortressRuntimeWorkshopCompletionNotifier _workshopCompletionNotifier = new();
    private readonly SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>> _runtimeSessionFactory;

    private SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>>? _runtimeSession;
    private FortressRuntimeContentSnapshot? _runtimeContentSnapshot;

    internal FortressRuntimeSessionCore(FortressRuntimeSessionCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _baseDir = options.BaseDir;
        _services = new RuntimeSessionServices();
        _strictContent = options.StrictContent;
        _contentWarningsAsErrors = options.ContentWarningsAsErrors;
        _log = options.Log ?? (_ => { });
        _createLogCallback = options.CreateLogCallback ?? (_ => _log);
        _logContentIssues = options.LogContentIssues ?? (_ => { });

        _runtimeSessionFactory = new SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>>(
            _services,
            LoadSessionContent,
            CreateRuntimeHost,
            () => NavigationTuning.LoadFromJson(_runtimeContentSnapshot?.NavigationTuningJson));
    }

    internal FortressRuntimeSessionCore(
        string baseDir,
        bool strictContent,
        bool contentWarningsAsErrors,
        Action<string>? log = null,
        Func<string, Action<string>>? createLogCallback = null,
        Action<FortressContentLoadResult>? logContentIssues = null)
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
