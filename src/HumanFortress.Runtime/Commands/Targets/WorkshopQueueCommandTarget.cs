using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.Placeables;

namespace HumanFortress.Runtime.Commands;

internal sealed partial class WorkshopQueueCommandTarget : IWorkshopQueueCommandTarget
{
    private const int CommandPriority = 50;
    private const string SystemId = "Runtime.WorkshopCommand";

    private readonly WorkshopDiffLog _workshopDiffLog;
    private readonly IRecipeCatalog _recipes;
    private readonly Action<string>? _log;

    internal WorkshopQueueCommandTarget(
        WorkshopDiffLog workshopDiffLog,
        IRecipeCatalog recipes,
        Action<string>? log = null)
    {
        _workshopDiffLog = workshopDiffLog ?? throw new ArgumentNullException(nameof(workshopDiffLog));
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _log = log;
    }

    bool IWorkshopQueueCommandTarget.AddWorkshopRecipe(Guid workshopGuid, string recipeId, ulong currentTick)
    {
        if (string.IsNullOrWhiteSpace(recipeId)) return false;

        var recipe = _recipes.GetRecipe(recipeId);
        if (recipe == null) return false;

        _workshopDiffLog.AddRecipe(workshopGuid, recipe.Id, recipe.Name, currentTick, CommandPriority, SystemId);
        _log?.Invoke($"[WORKSHOP] Queued add recipe workshop={workshopGuid} recipe={recipe.Id}");
        return true;
    }

    bool IWorkshopQueueCommandTarget.RemoveWorkshopQueueEntry(Guid workshopGuid, Guid entryId)
    {
        _workshopDiffLog.RemoveEntry(workshopGuid, entryId, CommandPriority, SystemId);
        _log?.Invoke($"[WORKSHOP] Queued remove entry workshop={workshopGuid} entry={entryId}");
        return true;
    }

    bool IWorkshopQueueCommandTarget.MoveWorkshopQueueEntry(Guid workshopGuid, Guid entryId, int moveOffset)
    {
        _workshopDiffLog.MoveEntry(workshopGuid, entryId, moveOffset, CommandPriority, SystemId);
        _log?.Invoke($"[WORKSHOP] Queued move entry workshop={workshopGuid} entry={entryId} offset={moveOffset}");
        return true;
    }

    bool IWorkshopQueueCommandTarget.ClearWorkshopQueue(Guid workshopGuid)
    {
        _workshopDiffLog.ClearQueue(workshopGuid, CommandPriority, SystemId);
        _log?.Invoke($"[WORKSHOP] Queued clear queue workshop={workshopGuid}");
        return true;
    }

    bool IWorkshopQueueCommandTarget.SetWorkshopWorkerSlots(Guid workshopGuid, int workerSlots)
    {
        _workshopDiffLog.SetWorkerSlots(workshopGuid, workerSlots, CommandPriority, SystemId);
        _log?.Invoke($"[WORKSHOP] Queued worker slots workshop={workshopGuid} slots={workerSlots}");
        return true;
    }

    bool IWorkshopQueueCommandTarget.SetWorkshopAutoStockpile(Guid workshopGuid, bool? value)
    {
        _workshopDiffLog.SetAutoStockpile(workshopGuid, value, CommandPriority, SystemId);
        _log?.Invoke($"[WORKSHOP] Queued auto-stockpile workshop={workshopGuid} value={value?.ToString() ?? "toggle"}");
        return true;
    }

    bool IWorkshopQueueCommandTarget.SetWorkshopAutoSupply(Guid workshopGuid, bool? value)
    {
        _workshopDiffLog.SetAutoSupply(workshopGuid, value, CommandPriority, SystemId);
        _log?.Invoke($"[WORKSHOP] Queued auto-supply workshop={workshopGuid} value={value?.ToString() ?? "toggle"}");
        return true;
    }
}
