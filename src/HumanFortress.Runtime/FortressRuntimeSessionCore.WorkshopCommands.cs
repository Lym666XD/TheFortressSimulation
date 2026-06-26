using HumanFortress.Runtime.Commands;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    void IFortressRuntimeSessionWorkshopCommandPort.QueueAddWorkshopRecipe(Guid workshopId, string recipeId)
    {
        EnqueueCurrentTickCommand(RuntimeWorkshopCommandFactory.AddRecipe(workshopId, recipeId));
    }

    void IFortressRuntimeSessionWorkshopCommandPort.QueueRemoveWorkshopQueueEntry(Guid workshopId, Guid entryId)
    {
        EnqueueCurrentTickCommand(RuntimeWorkshopCommandFactory.RemoveEntry(workshopId, entryId));
    }

    void IFortressRuntimeSessionWorkshopCommandPort.QueueMoveWorkshopQueueEntry(
        Guid workshopId,
        Guid entryId,
        int moveOffset)
    {
        EnqueueCurrentTickCommand(RuntimeWorkshopCommandFactory.MoveEntry(workshopId, entryId, moveOffset));
    }

    void IFortressRuntimeSessionWorkshopCommandPort.QueueSetWorkshopWorkerSlots(Guid workshopId, int workerSlots)
    {
        EnqueueCurrentTickCommand(RuntimeWorkshopCommandFactory.SetWorkerSlots(workshopId, workerSlots));
    }

    void IFortressRuntimeSessionWorkshopCommandPort.QueueToggleWorkshopAutoSupply(Guid workshopId)
    {
        EnqueueCurrentTickCommand(RuntimeWorkshopCommandFactory.ToggleAutoSupply(workshopId));
    }

    void IFortressRuntimeSessionWorkshopCommandPort.QueueToggleWorkshopAutoStockpile(Guid workshopId)
    {
        EnqueueCurrentTickCommand(RuntimeWorkshopCommandFactory.ToggleAutoStockpile(workshopId));
    }
}
