using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime.Commands;

internal static class RuntimeWorkshopCommandFactory
{
    internal static Func<ulong, ICommand> AddRecipe(Guid workshopId, string recipeId)
    {
        return tick => new UpdateWorkshopQueueCommand(
            tick,
            workshopId,
            WorkshopQueueOperation.AddRecipe,
            recipeId);
    }

    internal static Func<ulong, ICommand> RemoveEntry(Guid workshopId, Guid entryId)
    {
        return tick => new UpdateWorkshopQueueCommand(
            tick,
            workshopId,
            WorkshopQueueOperation.RemoveEntry,
            entryId: entryId);
    }

    internal static Func<ulong, ICommand> MoveEntry(Guid workshopId, Guid entryId, int moveOffset)
    {
        return tick => new UpdateWorkshopQueueCommand(
            tick,
            workshopId,
            WorkshopQueueOperation.MoveEntry,
            entryId: entryId,
            moveOffset: moveOffset);
    }

    internal static Func<ulong, ICommand> SetWorkerSlots(Guid workshopId, int workerSlots)
    {
        return tick => new UpdateWorkshopQueueCommand(
            tick,
            workshopId,
            WorkshopQueueOperation.SetWorkerSlots,
            intValue: workerSlots);
    }

    internal static Func<ulong, ICommand> ToggleAutoSupply(Guid workshopId)
    {
        return tick => new UpdateWorkshopQueueCommand(
            tick,
            workshopId,
            WorkshopQueueOperation.ToggleAutoSupply);
    }

    internal static Func<ulong, ICommand> ToggleAutoStockpile(Guid workshopId)
    {
        return tick => new UpdateWorkshopQueueCommand(
            tick,
            workshopId,
            WorkshopQueueOperation.ToggleAutoStockpile);
    }
}
