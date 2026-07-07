using HumanFortress.Contracts.Runtime.Replay;

namespace HumanFortress.Runtime;

internal interface IFortressRuntimeSessionReplayCheckpointPort
{
    RuntimeReplayCheckpointData GetReplayCheckpointData();
    string GetReplayCheckpointHash();
}
