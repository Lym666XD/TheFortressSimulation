using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationDebugSpawnData GetDebugSpawnData()
    {
        var debug = GetDebugMenuData().WorldStatus;
        return debug.HasWorld
            ? new SimulationDebugSpawnData(
                true,
                debug.ItemDefinitions,
                debug.CreatureDefinitions)
            : SimulationDebugSpawnData.Empty;
    }

    SimulationDebugSpawnData IFortressRuntimeDebugSpawnQueryAccess.GetDebugSpawnData() => GetDebugSpawnData();
}
