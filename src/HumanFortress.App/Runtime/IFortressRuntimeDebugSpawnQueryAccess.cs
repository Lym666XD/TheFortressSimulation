using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeDebugSpawnQueryAccess
{
    SimulationDebugSpawnData GetDebugSpawnData();
}
