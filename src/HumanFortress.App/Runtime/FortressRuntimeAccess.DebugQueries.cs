using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationDebugSpawnData GetDebugSpawnData()
    {
        return _snapshots.GetDebugSpawnData();
    }

    SimulationDebugSpawnData IFortressRuntimeDebugSpawnQueryAccess.GetDebugSpawnData() => GetDebugSpawnData();
}
