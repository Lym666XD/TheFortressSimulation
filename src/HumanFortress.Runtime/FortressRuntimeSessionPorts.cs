namespace HumanFortress.Runtime;

public interface IFortressRuntimeAppSessionPorts :
    IDisposable,
    IFortressRuntimeSessionLifecyclePort,
    IFortressRuntimeSessionBootstrapPort,
    IFortressRuntimeSessionCatalogQueryPort,
    IFortressRuntimeSessionReadPort,
    IFortressRuntimeSessionPlacementCommandPort,
    IFortressRuntimeSessionDebugCommandPort,
    IFortressRuntimeSessionSimulationControlPort,
    IFortressRuntimeSessionProfessionCommandPort,
    IFortressRuntimeSessionWorkshopCommandPort
{
}

internal interface IFortressRuntimeSessionPorts :
    IFortressRuntimeAppSessionPorts,
    IFortressRuntimeSessionSnapshotPort,
    IFortressRuntimeSessionReplayCheckpointPort,
    IFortressRuntimeSessionCheckpointPort,
    IFortressRuntimeSessionSaveManifestPort,
    IFortressRuntimeSessionSaveSnapshotPort
{
}
