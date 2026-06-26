using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Runtime.Snapshots;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    WorkforceDebugData IFortressRuntimeSessionSnapshotPort.GetWorkforceInputData()
    {
        return FortressRuntimeSessionSnapshotFacade.BuildWorkforceSnapshot(_runtimeSession);
    }

    SimulationWorkshopDebugData IFortressRuntimeSessionSnapshotPort.GetWorkshopDebugData()
    {
        return FortressRuntimeSessionSnapshotFacade.BuildWorkshopSnapshot(_runtimeSession);
    }

    WorkshopSummaryView? IFortressRuntimeSessionSnapshotPort.GetWorkshopPanelData(Guid workshopId)
    {
        return FortressRuntimeSessionSnapshotFacade.FindWorkshopSnapshot(_runtimeSession, workshopId);
    }

    string? IFortressRuntimeSessionSnapshotPort.GetDefaultRecipeForWorkshop(string? workshopId)
    {
        return FortressRuntimeSessionSnapshotFacade.GetDefaultRecipeForWorkshop(_runtimeSession, workshopId);
    }
}
