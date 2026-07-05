using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationDebugMenuData GetDebugMenuData()
    {
        return _snapshots.GetDebugMenuData();
    }

    internal WorkforceDebugData GetWorkforceInputData()
    {
        return _snapshots.GetWorkforceInputData();
    }

    SimulationDebugMenuData IFortressRuntimeUiInputAccess.GetDebugMenuData() => GetDebugMenuData();

    WorkforceDebugData IFortressRuntimeUiInputAccess.GetWorkforceInputData() => GetWorkforceInputData();
}
