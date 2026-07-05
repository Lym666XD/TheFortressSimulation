using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeWorkshopPanelQueryAccess
{
    WorkshopSummaryView? GetWorkshopPanelData(Guid workshopId);

    string? GetDefaultRecipeForWorkshop(string? workshopId);
}
