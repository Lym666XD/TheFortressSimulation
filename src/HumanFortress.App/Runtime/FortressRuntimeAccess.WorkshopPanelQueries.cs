using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal WorkshopSummaryView? GetWorkshopPanelData(Guid workshopId)
    {
        return _snapshots.GetWorkshopPanelData(workshopId);
    }

    internal string? GetDefaultRecipeForWorkshop(string? workshopId)
    {
        return _snapshots.GetDefaultRecipeForWorkshop(workshopId);
    }

    WorkshopSummaryView? IFortressRuntimeWorkshopPanelQueryAccess.GetWorkshopPanelData(Guid workshopId) =>
        GetWorkshopPanelData(workshopId);

    string? IFortressRuntimeWorkshopPanelQueryAccess.GetDefaultRecipeForWorkshop(string? workshopId) =>
        GetDefaultRecipeForWorkshop(workshopId);
}
