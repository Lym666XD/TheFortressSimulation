using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Runtime.Replay;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    RuntimeReplayCheckpointData IFortressRuntimeSessionReplayCheckpointPort.GetReplayCheckpointData()
    {
        return RuntimeReplayCheckpointHashBuilder.BuildData(_services, _runtimeSession);
    }

    string IFortressRuntimeSessionReplayCheckpointPort.GetReplayCheckpointHash()
    {
        return RuntimeReplayCheckpointHashBuilder.BuildData(_services, _runtimeSession).AggregateHash;
    }
}
