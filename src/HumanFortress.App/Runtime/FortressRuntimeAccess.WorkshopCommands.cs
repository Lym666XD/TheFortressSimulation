namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal void QueueAddWorkshopRecipe(Guid workshopId, string recipeId)
    {
        _workshopCommands.QueueAddWorkshopRecipe(workshopId, recipeId);
    }

    internal void QueueRemoveWorkshopQueueEntry(Guid workshopId, Guid entryId)
    {
        _workshopCommands.QueueRemoveWorkshopQueueEntry(workshopId, entryId);
    }

    internal void QueueMoveWorkshopQueueEntry(Guid workshopId, Guid entryId, int moveOffset)
    {
        _workshopCommands.QueueMoveWorkshopQueueEntry(workshopId, entryId, moveOffset);
    }

    internal void QueueSetWorkshopWorkerSlots(Guid workshopId, int workerSlots)
    {
        _workshopCommands.QueueSetWorkshopWorkerSlots(workshopId, workerSlots);
    }

    internal void QueueToggleWorkshopAutoSupply(Guid workshopId)
    {
        _workshopCommands.QueueToggleWorkshopAutoSupply(workshopId);
    }

    internal void QueueToggleWorkshopAutoStockpile(Guid workshopId)
    {
        _workshopCommands.QueueToggleWorkshopAutoStockpile(workshopId);
    }

    void IFortressRuntimeWorkshopPanelCommandAccess.QueueAddWorkshopRecipe(Guid workshopId, string recipeId) =>
        QueueAddWorkshopRecipe(workshopId, recipeId);

    void IFortressRuntimeWorkshopPanelCommandAccess.QueueRemoveWorkshopQueueEntry(Guid workshopId, Guid entryId) =>
        QueueRemoveWorkshopQueueEntry(workshopId, entryId);

    void IFortressRuntimeWorkshopPanelCommandAccess.QueueMoveWorkshopQueueEntry(Guid workshopId, Guid entryId, int moveOffset) =>
        QueueMoveWorkshopQueueEntry(workshopId, entryId, moveOffset);

    void IFortressRuntimeWorkshopPanelCommandAccess.QueueSetWorkshopWorkerSlots(Guid workshopId, int workerSlots) =>
        QueueSetWorkshopWorkerSlots(workshopId, workerSlots);

    void IFortressRuntimeWorkshopPanelCommandAccess.QueueToggleWorkshopAutoSupply(Guid workshopId) =>
        QueueToggleWorkshopAutoSupply(workshopId);

    void IFortressRuntimeWorkshopPanelCommandAccess.QueueToggleWorkshopAutoStockpile(Guid workshopId) =>
        QueueToggleWorkshopAutoStockpile(workshopId);
}
