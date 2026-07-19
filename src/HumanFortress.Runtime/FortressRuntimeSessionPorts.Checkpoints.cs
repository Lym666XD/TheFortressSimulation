using HumanFortress.Contracts.Runtime.Checkpoints;

namespace HumanFortress.Runtime;

internal interface IFortressRuntimeSessionCheckpointPort
{
    bool TryGetLatestCheckpointIdentity(out RuntimeCheckpointIdentityData identity);

    bool TryGetLatestCommittedDiagnostics(out RuntimeCommittedDiagnosticsData diagnostics);
}
