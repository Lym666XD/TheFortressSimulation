using HumanFortress.Runtime;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Narrow App-owned port adapter over the Runtime session core.
/// </summary>
internal sealed partial class FortressRuntimeAccess : IFortressRuntimeSessionAccess
{
    private readonly IFortressRuntimeSessionBootstrapPort _bootstrap;
    private readonly IFortressRuntimeSessionReadPort _read;
    private readonly IFortressRuntimeSessionSnapshotPort _snapshots;
    private readonly IFortressRuntimeSessionPlacementCommandPort _placementCommands;
    private readonly IFortressRuntimeSessionDebugCommandPort _debugCommands;
    private readonly IFortressRuntimeSessionSimulationControlPort _simulationControl;
    private readonly IFortressRuntimeSessionProfessionCommandPort _professionCommands;
    private readonly IFortressRuntimeSessionWorkshopCommandPort _workshopCommands;
    private readonly IFortressRuntimeSessionSaveSnapshotPort _saveSnapshots;

    internal FortressRuntimeAccess(IFortressRuntimeSessionPorts runtimeSession)
    {
        ArgumentNullException.ThrowIfNull(runtimeSession);

        _bootstrap = runtimeSession;
        _read = runtimeSession;
        _snapshots = runtimeSession;
        _placementCommands = runtimeSession;
        _debugCommands = runtimeSession;
        _simulationControl = runtimeSession;
        _professionCommands = runtimeSession;
        _workshopCommands = runtimeSession;
        _saveSnapshots = runtimeSession;
    }
}
