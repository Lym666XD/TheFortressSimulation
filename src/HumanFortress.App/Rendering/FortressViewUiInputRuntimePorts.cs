using HumanFortress.App.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Rendering;

internal sealed class FortressViewUiInputRuntimePorts
{
    private readonly IFortressRuntimeUiInputAccess _runtime;

    internal FortressViewUiInputRuntimePorts(IFortressRuntimeUiInputAccess runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    internal SimulationDebugMenuData GetDebugMenuData() =>
        _runtime.GetDebugMenuData();

    internal WorkforceDebugData GetWorkforceInputData() =>
        _runtime.GetWorkforceInputData();

    internal void SetProfessionWeight(Guid workerId, string professionId, int weight) =>
        _runtime.SetProfessionWeight(workerId, professionId, weight);
}
