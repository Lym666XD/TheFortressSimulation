using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationPlacementPreviewData GetPlacementPreviewData(
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode)
    {
        return _read.GetPlacementPreviewData(
            first.ToRuntimePoint(),
            second.ToRuntimePoint(),
            z,
            mode);
    }

    SimulationPlacementPreviewData IFortressRuntimeReadAccess.GetPlacementPreviewData(
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode) =>
        GetPlacementPreviewData(first, second, z, mode);
}
