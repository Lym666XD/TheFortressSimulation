using HumanFortress.Runtime;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Narrow App-owned port adapter over the Runtime session core.
/// </summary>
internal sealed partial class FortressRuntimeAccess :
    IFortressRuntimeReadAccess,
    IFortressRuntimeMapInspectionAccess,
    IFortressRuntimeDebugSpawnQueryAccess,
    IFortressRuntimeDebugSpawnCommandAccess,
    IFortressRuntimeBuildCatalogAccess,
    IFortressRuntimeZoneCatalogAccess,
    IFortressRuntimeWorkshopPanelQueryAccess,
    IFortressRuntimeWorkshopPanelCommandAccess,
    IFortressRuntimeNavigationDebugAccess,
    IFortressRuntimeSimulationControlAccess,
    IFortressRuntimeUiInputAccess,
    IFortressRuntimePlacementQueryAccess,
    IFortressRuntimePlacementCommandAccess,
    IFortressRuntimeBootstrapAccess
{
    private readonly IFortressRuntimeSessionBootstrapPort _bootstrap;
    private readonly IFortressRuntimeSessionCatalogQueryPort _catalog;
    private readonly IFortressRuntimeSessionReadPort _read;
    private readonly IFortressRuntimeSessionPlacementCommandPort _placementCommands;
    private readonly IFortressRuntimeSessionDebugCommandPort _debugCommands;
    private readonly IFortressRuntimeSessionSimulationControlPort _simulationControl;
    private readonly IFortressRuntimeSessionProfessionCommandPort _professionCommands;
    private readonly IFortressRuntimeSessionWorkshopCommandPort _workshopCommands;

    internal FortressRuntimeAccess(IFortressRuntimeAppSessionPorts runtimeSession)
    {
        ArgumentNullException.ThrowIfNull(runtimeSession);

        _bootstrap = runtimeSession;
        _catalog = runtimeSession;
        _read = runtimeSession;
        _placementCommands = runtimeSession;
        _debugCommands = runtimeSession;
        _simulationControl = runtimeSession;
        _professionCommands = runtimeSession;
        _workshopCommands = runtimeSession;
    }
}
