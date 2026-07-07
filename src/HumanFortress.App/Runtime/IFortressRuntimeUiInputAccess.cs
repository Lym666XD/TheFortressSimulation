using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeUiInputAccess
{
    SimulationDebugMenuData GetDebugMenuData();
    WorkforceDebugData GetWorkforceInputData();
    void SetProfessionWeight(Guid workerId, string professionId, int weight);
}
