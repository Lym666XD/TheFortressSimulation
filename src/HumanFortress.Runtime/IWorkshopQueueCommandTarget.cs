namespace HumanFortress.Runtime;

public interface IWorkshopQueueCommandTarget
{
    bool AddWorkshopRecipe(Guid workshopGuid, string recipeId, ulong currentTick);

    bool RemoveWorkshopQueueEntry(Guid workshopGuid, Guid entryId);

    bool MoveWorkshopQueueEntry(Guid workshopGuid, Guid entryId, int moveOffset);

    bool ClearWorkshopQueue(Guid workshopGuid);

    bool SetWorkshopWorkerSlots(Guid workshopGuid, int workerSlots);

    bool SetWorkshopAutoStockpile(Guid workshopGuid, bool? value);

    bool SetWorkshopAutoSupply(Guid workshopGuid, bool? value);
}
