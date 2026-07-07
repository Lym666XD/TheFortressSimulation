namespace HumanFortress.Runtime;

public interface IFortressRuntimeAppSessionPorts :
    IFortressRuntimeSessionLifecyclePort,
    IFortressRuntimeSessionBootstrapPort,
    IFortressRuntimeSessionReadPort,
    IFortressRuntimeSessionSnapshotPort,
    IFortressRuntimeSessionPlacementCommandPort,
    IFortressRuntimeSessionDebugCommandPort,
    IFortressRuntimeSessionSimulationControlPort,
    IFortressRuntimeSessionProfessionCommandPort,
    IFortressRuntimeSessionWorkshopCommandPort
{
}

internal interface IFortressRuntimeSessionPorts :
    IFortressRuntimeAppSessionPorts,
    IFortressRuntimeSessionReplayCheckpointPort,
    IFortressRuntimeSessionSaveManifestPort,
    IFortressRuntimeSessionSaveSnapshotPort
{
}
