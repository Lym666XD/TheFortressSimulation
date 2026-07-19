using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal WorkshopSummaryView? GetWorkshopPanelData(Guid workshopId)
    {
        if (!TryGetCommittedFrame(out var committed))
            return null;

        var workshop = committed.Frame.UiOverlay.Workshops.Workshops
            .FirstOrDefault(candidate => candidate.WorkshopGuid == workshopId);
        return workshop.WorkshopGuid == Guid.Empty ? null : workshop;
    }

    internal string? GetDefaultRecipeForWorkshop(string? workshopId)
    {
        return _catalog.GetDefaultRecipeForWorkshop(workshopId);
    }

    WorkshopSummaryView? IFortressRuntimeWorkshopPanelQueryAccess.GetWorkshopPanelData(Guid workshopId) =>
        GetWorkshopPanelData(workshopId);

    string? IFortressRuntimeWorkshopPanelQueryAccess.GetDefaultRecipeForWorkshop(string? workshopId) =>
        GetDefaultRecipeForWorkshop(workshopId);
}
