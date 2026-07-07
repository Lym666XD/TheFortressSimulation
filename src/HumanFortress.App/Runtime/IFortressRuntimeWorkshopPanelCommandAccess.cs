namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeWorkshopPanelCommandAccess
{
    void QueueAddWorkshopRecipe(Guid workshopId, string recipeId);

    void QueueRemoveWorkshopQueueEntry(Guid workshopId, Guid entryId);

    void QueueMoveWorkshopQueueEntry(Guid workshopId, Guid entryId, int moveOffset);

    void QueueSetWorkshopWorkerSlots(Guid workshopId, int workerSlots);

    void QueueToggleWorkshopAutoSupply(Guid workshopId);

    void QueueToggleWorkshopAutoStockpile(Guid workshopId);
}
